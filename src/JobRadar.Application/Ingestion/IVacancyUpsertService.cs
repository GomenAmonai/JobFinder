using JobRadar.Domain.Entities;

namespace JobRadar.Application.Ingestion;

/// <summary>
/// Идемпотентный приём вакансии: повторная доставка той же (Source, ExternalId)
/// обновляет запись, а не создаёт дубль, — даже когда несколько потребителей
/// пишут одновременно. Центральный showpiece проекта.
/// </summary>
public interface IVacancyUpsertService
{
    Task<UpsertOutcome> UpsertAsync(Vacancy incoming, CancellationToken ct = default);
}

public enum UpsertOutcome { Inserted, Updated }
