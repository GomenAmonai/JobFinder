namespace JobRadar.Application.Ingestion;

/// <summary>
/// Сообщение, которое не удалось обработать, отправляется в dead-letter топик
/// вместо тихого пропуска: сохраняем исходное тело, причину и позицию, чтобы потом
/// можно было разобрать и переиграть, а не потерять.
/// </summary>
public sealed record DeadLetterEnvelope
{
    public required string SourceTopic { get; init; }
    public string? Key { get; init; }
    public required string Payload { get; init; }
    public required string Error { get; init; }
    public required string Offset { get; init; }
    public required DateTimeOffset FailedAt { get; init; }
}
