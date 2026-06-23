namespace JobRadar.Application.Ingestion;

/// <summary>
/// Событие в топике <c>vacancies.changed</c>: воркер публикует его после каждого
/// upsert, а API раздаёт подключённым SignalR-клиентам. Так воркер и API,
/// будучи разными процессами, остаются развязанными — без in-process HubContext.
/// Ключ = "{Source}:{ExternalId}".
/// </summary>
public sealed record VacancyChangedEvent
{
    public required string Source { get; init; }
    public required string ExternalId { get; init; }
    public required string Title { get; init; }
    public string? Company { get; init; }
    public string? Market { get; init; }
    public string? Level { get; init; }
    public string? Stack { get; init; }
    public string? Url { get; init; }
    public DateTimeOffset? PublishedAt { get; init; }
    public required UpsertOutcome Outcome { get; init; }
}
