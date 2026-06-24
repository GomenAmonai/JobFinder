using JobRadar.Application.Vacancies;
using JobRadar.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace JobRadar.Infrastructure.Vacancies;

public sealed class VacancyQueryService(JobRadarDbContext db) : IVacancyQueryService
{
    private const int MaxPageSize = 100;
    private const int MaxSearchLength = 100;

    public async Task<PagedResult<VacancyDto>> SearchAsync(VacancyQuery query, CancellationToken ct = default)
    {
        var page = Math.Max(1, query.Page);
        var pageSize = Math.Clamp(query.PageSize, 1, MaxPageSize);

        var filtered = db.Vacancies.AsNoTracking();

        if (!string.IsNullOrWhiteSpace(query.Market))
            filtered = filtered.Where(v => v.Market == query.Market);
        if (!string.IsNullOrWhiteSpace(query.Level))
            filtered = filtered.Where(v => v.Level == query.Level);
        if (!string.IsNullOrWhiteSpace(query.Stack))
            filtered = filtered.Where(v => v.Stack == query.Stack);
        if (!string.IsNullOrWhiteSpace(query.Q))
        {
            // Экранируем спецсимволы LIKE (% _ \) и ограничиваем длину: иначе
            // ведущий wildcard на неиндексированных колонках навязывает дорогой
            // seq-scan — дешёвый DoS на анонимном эндпоинте.
            var trimmed = query.Q.Trim();
            if (trimmed.Length > MaxSearchLength)
                trimmed = trimmed[..MaxSearchLength];
            var escaped = trimmed.Replace("\\", "\\\\").Replace("%", "\\%").Replace("_", "\\_");
            var term = $"%{escaped}%";
            filtered = filtered.Where(v =>
                EF.Functions.ILike(v.Title, term, "\\")
                || (v.Company != null && EF.Functions.ILike(v.Company, term, "\\"))
                || (v.Skills != null && EF.Functions.ILike(v.Skills, term, "\\")));
        }

        // Сворачиваем кросс-источниковые дубли ПОСЛЕ фильтров: канон (минимальный Id =
        // первый увиденный) выбирается среди строк, уже прошедших фильтр. Так дубль,
        // совпавший по грани, которой нет у первой строки (напр. Market), не теряется.
        // Записи без ключа дедупликации проходят как есть.
        var canonicalIds = filtered
            .Where(v => v.DedupKey != null)
            .GroupBy(v => v.DedupKey)
            .Select(g => g.Min(x => x.Id));
        filtered = filtered.Where(v => v.DedupKey == null || canonicalIds.Contains(v.Id));

        var total = await filtered.CountAsync(ct);

        var items = await filtered
            // NULLS LAST: вакансии без даты публикации уходят в конец.
            .OrderByDescending(v => v.PublishedAt.HasValue)
            .ThenByDescending(v => v.PublishedAt)
            .ThenByDescending(v => v.Id)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(v => new VacancyDto
            {
                Id = v.Id,
                Source = v.Source,
                ExternalId = v.ExternalId,
                Title = v.Title,
                Company = v.Company,
                Market = v.Market,
                Level = v.Level,
                Stack = v.Stack,
                Location = v.Location,
                SalaryRaw = v.SalaryRaw,
                SalaryMin = v.SalaryMin,
                SalaryMax = v.SalaryMax,
                SalaryCurrency = v.SalaryCurrency,
                Skills = v.Skills,
                Url = v.Url,
                PublishedAt = v.PublishedAt,
                FirstSeen = v.FirstSeen,
                LastSeen = v.LastSeen,
            })
            .ToListAsync(ct);

        return new PagedResult<VacancyDto>(items, page, pageSize, total);
    }
}
