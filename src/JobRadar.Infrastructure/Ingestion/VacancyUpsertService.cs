using JobRadar.Application.Ingestion;
using JobRadar.Domain.Entities;
using JobRadar.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace JobRadar.Infrastructure.Ingestion;

/// <summary>
/// Идемпотентный upsert вакансии под конкурентной нагрузкой. Идемпотентность
/// держится на уникальном индексе (Source, ExternalId): дубль физически не может
/// возникнуть. Стратегия «update-first»:
///   1. Атомарный set-based UPDATE (частый случай — вакансию уже видели).
///      ExecuteUpdate не читает строку и не использует concurrency-token, поэтому
///      под высокой конкуренцией не вырождается в шторм ретраев (last-writer-wins —
///      корректно: повторные доставки одной вакансии несут те же данные).
///   2. Строки нет (0 затронуто) — INSERT. Если конкурент успел вставить её между
///      нашим UPDATE и INSERT, ловим нарушение уникального индекса (Postgres 23505)
///      и повторяем цикл: следующий UPDATE подхватит чужую строку, а если её к тому
///      моменту удалили — INSERT пройдёт. Так возвращаемый исход всегда честный.
/// FirstSeen ставится только при вставке и сохраняется при обновлениях.
/// </summary>
public sealed class VacancyUpsertService(JobRadarDbContext db, TimeProvider clock) : IVacancyUpsertService
{
    public async Task<UpsertOutcome> UpsertAsync(Vacancy incoming, CancellationToken ct = default)
    {
        var now = clock.GetUtcNow();

        while (true)
        {
            db.ChangeTracker.Clear();

            if (await UpdateExistingAsync(incoming, now, ct) > 0)
                return UpsertOutcome.Updated;

            try
            {
                incoming.FirstSeen = now;
                incoming.LastSeen = now;
                db.Vacancies.Add(incoming);
                await db.SaveChangesAsync(ct);
                return UpsertOutcome.Inserted;
            }
            catch (DbUpdateException ex) when (IsUniqueViolation(ex))
            {
                // Конкурент опередил наш INSERT — на следующей итерации обновим его строку.
            }
        }
    }

    private Task<int> UpdateExistingAsync(Vacancy incoming, DateTimeOffset now, CancellationToken ct)
        => db.Vacancies
            .Where(v => v.Source == incoming.Source && v.ExternalId == incoming.ExternalId)
            .ExecuteUpdateAsync(s => s
                .SetProperty(v => v.Title, incoming.Title)
                .SetProperty(v => v.Company, incoming.Company)
                .SetProperty(v => v.Market, incoming.Market)
                .SetProperty(v => v.Level, incoming.Level)
                .SetProperty(v => v.Stack, incoming.Stack)
                .SetProperty(v => v.Location, incoming.Location)
                .SetProperty(v => v.SalaryRaw, incoming.SalaryRaw)
                .SetProperty(v => v.SalaryMin, incoming.SalaryMin)
                .SetProperty(v => v.SalaryMax, incoming.SalaryMax)
                .SetProperty(v => v.SalaryCurrency, incoming.SalaryCurrency)
                .SetProperty(v => v.Skills, incoming.Skills)
                .SetProperty(v => v.Url, incoming.Url)
                .SetProperty(v => v.PublishedAt, incoming.PublishedAt)
                .SetProperty(v => v.LastSeen, now), ct);

    private static bool IsUniqueViolation(DbUpdateException ex)
        => ex.InnerException is PostgresException { SqlState: PostgresErrorCodes.UniqueViolation };
}
