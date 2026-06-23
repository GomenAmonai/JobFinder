using JobRadar.Application.Ingestion;

namespace JobRadar.Worker.Ingestion;

public interface IVacancyMessageProducer
{
    Task ProduceAsync(RawVacancyMessage message, CancellationToken ct);
}
