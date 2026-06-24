using JobRadar.Application.Applications;
using JobRadar.Application.Employer;
using JobRadar.Application.Ingestion;
using JobRadar.Application.Vacancies;
using JobRadar.Domain.Entities;
using JobRadar.Infrastructure.Applications;
using JobRadar.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace JobRadar.Infrastructure.Employer;

/// <summary>
/// Сторона работодателя: постит нативные вакансии на JobRadar (Source=JobRadar,
/// PostedByUserId) и управляет статусами входящих откликов на СВОИ вакансии. Чужие
/// вакансии не видит и не трогает (scoped по employerId). Отзыв отклика —
/// исключительно право кандидата, работодателю запрещён.
/// </summary>
public sealed class EmployerService(JobRadarDbContext db, TimeProvider clock) : IEmployerService
{
    private const string NativeSource = "JobRadar";
    private const string XminProperty = "xmin";

    public async Task<VacancyDto> PostVacancyAsync(Guid employerId, CreateVacancyRequest request, CancellationToken ct = default)
    {
        var now = clock.GetUtcNow();
        var classifyText = $"{request.Title} {request.Skills}";
        var salary = SalaryParser.Parse(request.SalaryRaw);
        var vacancy = new Vacancy
        {
            Source = NativeSource,
            ExternalId = Guid.NewGuid().ToString(),
            Title = request.Title,
            Company = request.Company,
            Location = request.Location,
            Market = VacancyNormalization.NormalizeMarket(request.Location),
            Level = VacancyNormalization.GuessLevel(classifyText),
            Stack = VacancyNormalization.DetectStack(classifyText),
            SalaryRaw = request.SalaryRaw,
            SalaryMin = salary.Min,
            SalaryMax = salary.Max,
            SalaryCurrency = salary.Currency,
            Skills = request.Skills,
            Url = request.Url,
            PublishedAt = now,
            FirstSeen = now,
            LastSeen = now,
            PostedByUserId = employerId,
            // Нативную вакансию в дедупликацию НЕ включаем (DedupKey=null → всегда видна
            // в ленте): иначе совпавшая агрегированная вакансия с меньшим Id вытеснила бы
            // её как каноническую, и отклики работодателю уходили бы «в никуда».
            DedupKey = null,
        };
        db.Vacancies.Add(vacancy);
        await db.SaveChangesAsync(ct);
        return ToVacancyDto(vacancy);
    }

    public async Task<IReadOnlyList<EmployerApplicationDto>> ListApplicationsAsync(Guid employerId, CancellationToken ct = default)
    {
        var rows = await (
            from a in db.Applications.Include(x => x.Vacancy)
            join u in db.Users on a.UserId equals u.Id
            where a.Vacancy!.PostedByUserId == employerId
            orderby a.UpdatedAt descending
            select new { App = a, u.Email, u.DisplayName }).ToListAsync(ct);

        return rows.Select(r => ToEmployerDto(r.App, CurrentVersion(r.App), r.Email, r.DisplayName)).ToList();
    }

    public async Task<EmployerStatusChangeOutcome> ChangeApplicationStatusAsync(Guid employerId, int applicationId, UpdateApplicationStatusRequest request, CancellationToken ct = default)
    {
        var application = await db.Applications
            .Include(a => a.Vacancy)
            .SingleOrDefaultAsync(a => a.Id == applicationId, ct);
        // Чужой/несуществующий отклик неотличим — не раскрываем существование.
        if (application is null || application.Vacancy!.PostedByUserId != employerId)
            return new EmployerStatusChangeOutcome(StatusChangeResult.NotFound, null, null);
        if (request.Status == ApplicationStatus.Withdrawn ||
            !ApplicationStatusTransitions.CanTransition(application.Status, request.Status))
            return new EmployerStatusChangeOutcome(StatusChangeResult.IllegalTransition, null, null);
        if (!uint.TryParse(request.Version, out var clientVersion))
            return new EmployerStatusChangeOutcome(StatusChangeResult.Conflict, null, null);

        application.Status = request.Status;
        application.UpdatedAt = clock.GetUtcNow();
        db.Entry(application).Property(XminProperty).OriginalValue = clientVersion;

        try
        {
            await db.SaveChangesAsync(ct);
            return new EmployerStatusChangeOutcome(StatusChangeResult.Changed,
                ApplicationDtoMapper.ToDto(application, CurrentVersion(application)), application.UserId);
        }
        catch (DbUpdateConcurrencyException)
        {
            return new EmployerStatusChangeOutcome(StatusChangeResult.Conflict, null, null);
        }
    }

    private uint CurrentVersion(JobApplication application)
        => (uint)db.Entry(application).Property(XminProperty).CurrentValue!;

    private static EmployerApplicationDto ToEmployerDto(JobApplication a, uint version, string candidateEmail, string? candidateDisplayName) => new()
    {
        Id = a.Id,
        Status = a.Status,
        CoverLetter = a.CoverLetter,
        Version = version.ToString(),
        CreatedAt = a.CreatedAt,
        UpdatedAt = a.UpdatedAt,
        CandidateEmail = candidateEmail,
        CandidateDisplayName = candidateDisplayName,
        Vacancy = new ApplicationVacancySummary
        {
            Id = a.Vacancy!.Id,
            Title = a.Vacancy.Title,
            Company = a.Vacancy.Company,
            Url = a.Vacancy.Url,
            Market = a.Vacancy.Market,
            Level = a.Vacancy.Level,
        },
    };

    private static VacancyDto ToVacancyDto(Vacancy v) => new()
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
    };
}
