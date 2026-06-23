namespace JobRadar.Application.Ingestion;

/// <summary>
/// Контракт сообщения в топике Kafka <c>vacancies.raw</c>. Это граница между
/// коллекторами (на любом языке) и нормализатором: коллектор отдаёт сырьё как
/// есть, а классификацию (рынок/грейд/стек) и дедуп делает потребитель.
/// Ключ сообщения = "{Source}:{ExternalId}" — одна и та же вакансия всегда
/// попадает в один партишн, поэтому порядок её апдейтов сохраняется.
/// </summary>
public sealed record RawVacancyMessage
{
    public required string Source { get; init; }
    public required string ExternalId { get; init; }
    public required string Title { get; init; }
    public string? Company { get; init; }
    public string? Location { get; init; }
    public string? SalaryRaw { get; init; }
    public string? Skills { get; init; }
    public string? Url { get; init; }
    public DateTimeOffset? PublishedAt { get; init; }
}
