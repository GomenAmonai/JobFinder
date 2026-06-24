using JobRadar.Application.Applications;
using JobRadar.Domain.Entities;
using JobRadar.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace JobRadar.Infrastructure.Applications;

/// <summary>
/// Трекер откликов кандидата. Откликнуться дважды нельзя — это держит уникальный
/// индекс (UserId, VacancyId): на гонке вставки ловим нарушение (Postgres 23505) и
/// возвращаем AlreadyApplied вместо дубля (тот же приём идемпотентности, что и в
/// приёме вакансий). Смена статуса валидируется как переход
/// (<see cref="ApplicationStatusTransitions"/>) и идёт под optimistic concurrency
/// через xmin: устаревшая клиентская Version даёт конфликт, а не тихую перезапись.
/// </summary>
public sealed class ApplicationService(JobRadarDbContext db, TimeProvider clock) : IApplicationService
{
    private const string XminProperty = "xmin";

    public async Task<ApplyOutcome> ApplyAsync(Guid userId, int vacancyId, CreateApplicationRequest request, CancellationToken ct = default)
    {
        var vacancy = await db.Vacancies.SingleOrDefaultAsync(v => v.Id == vacancyId, ct);
        if (vacancy is null)
            return new ApplyOutcome(ApplyResult.VacancyNotFound, null);

        var now = clock.GetUtcNow();
        var application = new JobApplication
        {
            UserId = userId,
            VacancyId = vacancyId,
            Vacancy = vacancy,
            Status = ApplicationStatus.Submitted,
            CoverLetter = Normalize(request.CoverLetter),
            CreatedAt = now,
            UpdatedAt = now,
        };
        db.Applications.Add(application);

        try
        {
            await db.SaveChangesAsync(ct);
        }
        catch (DbUpdateException ex) when (IsUniqueViolation(ex))
        {
            return new ApplyOutcome(ApplyResult.AlreadyApplied, null);
        }

        return new ApplyOutcome(ApplyResult.Created, ToDto(application, CurrentVersion(application)));
    }

    public async Task<IReadOnlyList<ApplicationDto>> ListAsync(Guid userId, CancellationToken ct = default)
    {
        var applications = await db.Applications
            .Include(a => a.Vacancy)
            .Where(a => a.UserId == userId)
            .OrderByDescending(a => a.UpdatedAt)
            .ToListAsync(ct);

        return applications.Select(a => ToDto(a, CurrentVersion(a))).ToList();
    }

    public async Task<ApplicationDto?> GetAsync(Guid userId, int id, CancellationToken ct = default)
    {
        var application = await db.Applications
            .Include(a => a.Vacancy)
            .SingleOrDefaultAsync(a => a.Id == id && a.UserId == userId, ct);

        return application is null ? null : ToDto(application, CurrentVersion(application));
    }

    public async Task<StatusChangeOutcome> ChangeStatusAsync(Guid userId, int id, UpdateApplicationStatusRequest request, CancellationToken ct = default)
    {
        var application = await db.Applications
            .Include(a => a.Vacancy)
            .SingleOrDefaultAsync(a => a.Id == id && a.UserId == userId, ct);
        if (application is null)
            return new StatusChangeOutcome(StatusChangeResult.NotFound, null);
        // На нативной вакансии (postedBy != null) стадии двигает работодатель; кандидат
        // может только отозвать отклик. На агрегированной — это личный трекер, кандидат
        // ведёт все стадии сам.
        if (application.Vacancy!.PostedByUserId is not null && request.Status != ApplicationStatus.Withdrawn)
            return new StatusChangeOutcome(StatusChangeResult.IllegalTransition, null);
        if (!ApplicationStatusTransitions.CanTransition(application.Status, request.Status))
            return new StatusChangeOutcome(StatusChangeResult.IllegalTransition, null);
        if (!uint.TryParse(request.Version, out var clientVersion))
            return new StatusChangeOutcome(StatusChangeResult.Conflict, null);

        application.Status = request.Status;
        application.UpdatedAt = clock.GetUtcNow();

        // Проверяем против версии, которую видел клиент, а не свежезагруженной.
        db.Entry(application).Property(XminProperty).OriginalValue = clientVersion;

        try
        {
            await db.SaveChangesAsync(ct);
            return new StatusChangeOutcome(StatusChangeResult.Changed, ToDto(application, CurrentVersion(application)));
        }
        catch (DbUpdateConcurrencyException)
        {
            return new StatusChangeOutcome(StatusChangeResult.Conflict, null);
        }
    }

    public async Task<bool> DeleteAsync(Guid userId, int id, CancellationToken ct = default)
    {
        var deleted = await db.Applications
            .Where(a => a.Id == id && a.UserId == userId)
            .ExecuteDeleteAsync(ct);
        return deleted > 0;
    }

    private static string? Normalize(string? coverLetter)
        => string.IsNullOrWhiteSpace(coverLetter) ? null : coverLetter.Trim();

    private uint CurrentVersion(JobApplication application)
        => (uint)db.Entry(application).Property(XminProperty).CurrentValue!;

    private static ApplicationDto ToDto(JobApplication a, uint version) => ApplicationDtoMapper.ToDto(a, version);

    private static bool IsUniqueViolation(DbUpdateException ex)
        => ex.InnerException is PostgresException { SqlState: PostgresErrorCodes.UniqueViolation };
}
