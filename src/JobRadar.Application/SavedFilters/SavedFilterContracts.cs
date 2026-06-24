namespace JobRadar.Application.SavedFilters;

/// <summary>
/// Version — значение xmin строки на момент чтения. Клиент возвращает его при
/// обновлении; несовпадение = конкурентная правка → конфликт (optimistic concurrency).
/// </summary>
public sealed record SavedFilterDto
{
    public required int Id { get; init; }
    public required string Name { get; init; }
    public string? Market { get; init; }
    public string? Level { get; init; }
    public string? Stack { get; init; }
    public string? Q { get; init; }
    public required string Version { get; init; }
    public required DateTimeOffset CreatedAt { get; init; }
    public required DateTimeOffset UpdatedAt { get; init; }
}

public sealed record CreateSavedFilterRequest(string Name, string? Market, string? Level, string? Stack, string? Q);

public sealed record UpdateSavedFilterRequest(string Name, string? Market, string? Level, string? Stack, string? Q, string Version);

public enum SavedFilterUpdateStatus { Updated, NotFound, Conflict }

public sealed record SavedFilterUpdateOutcome(SavedFilterUpdateStatus Status, SavedFilterDto? Filter);
