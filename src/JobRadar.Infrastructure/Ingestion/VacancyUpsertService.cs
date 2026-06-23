using JobRadar.Application.Ingestion;
using JobRadar.Domain.Entities;
using JobRadar.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace JobRadar.Infrastructure.Ingestion;

/// <summary>
/// Идемпотентный upsert вакансии под конкурентной нагрузкой. Два независимых
/// механизма гонок:
///   1. Уникальный индекс (Source, ExternalId) — отклоняет конкурентный INSERT
///      дубля (Postgres 23505).
///   2. xmin как concurrency token — ловит конкурентный UPDATE той же строки
///      (DbUpdateConcurrencyException), когда строку изменили между нашим
///      чтением и записью.
/// В обоих случаях перечитываем актуальное состояние и накатываем изменения
/// заново. Итог: ровно одна строка на (Source, ExternalId), без потери апдейтов.
/// </summary>
public sealed class VacancyUpsertService(JobRadarDbContext db, TimeProvider clock) : IVacancyUpsertService
{
    private const int MaxAttempts = 5;

    public async Task<UpsertResult> UpsertAsync(Vacancy incoming, CancellationToken ct = default)
    {
        for (var attempt = 1; ; attempt++)
        {
            // Каждая попытка стартует с чистого трекера: предыдущий неудачный
            // INSERT/UPDATE не должен тянуться в следующую итерацию.
            db.ChangeTracker.Clear();

            var existing = await db.Vacancies.SingleOrDefaultAsync(
                v => v.Source == incoming.Source && v.ExternalId == incoming.ExternalId, ct);
            var now = clock.GetUtcNow();

            try
            {
                if (existing is null)
                {
                    incoming.FirstSeen = now;
                    incoming.LastSeen = now;
                    db.Vacancies.Add(incoming);
                    await db.SaveChangesAsync(ct);
                    return new UpsertResult(UpsertOutcome.Inserted, incoming.Id);
                }

                ApplyMutableFields(from: incoming, to: existing);
                existing.LastSeen = now;
                await db.SaveChangesAsync(ct);
                return new UpsertResult(UpsertOutcome.Updated, existing.Id);
            }
            catch (DbUpdateException ex)
                when (attempt < MaxAttempts && (ex is DbUpdateConcurrencyException || IsUniqueViolation(ex)))
            {
                // Конкурентный писатель опередил нас (дубль-INSERT или гонка UPDATE) —
                // перечитываем на следующей итерации и повторяем.
            }
        }
    }

    private static bool IsUniqueViolation(DbUpdateException ex)
        => ex.InnerException is PostgresException { SqlState: PostgresErrorCodes.UniqueViolation };

    private static void ApplyMutableFields(Vacancy from, Vacancy to)
    {
        to.Title = from.Title;
        to.Company = from.Company;
        to.Market = from.Market;
        to.Level = from.Level;
        to.Stack = from.Stack;
        to.Location = from.Location;
        to.SalaryRaw = from.SalaryRaw;
        to.SalaryMin = from.SalaryMin;
        to.SalaryMax = from.SalaryMax;
        to.SalaryCurrency = from.SalaryCurrency;
        to.Skills = from.Skills;
        to.Url = from.Url;
        to.PublishedAt = from.PublishedAt;
    }
}
