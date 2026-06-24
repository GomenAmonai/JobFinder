using JobRadar.Domain.Entities;

namespace JobRadar.Application.Applications;

/// <summary>
/// Version — значение xmin строки на момент чтения; клиент возвращает его при смене
/// статуса, несовпадение = конкурентная правка → конфликт (optimistic concurrency).
/// Vacancy — срез вакансии, чтобы список откликов был самодостаточным без N+1.
/// </summary>
public sealed record ApplicationDto
{
    public required int Id { get; init; }
    public required ApplicationStatus Status { get; init; }
    public string? CoverLetter { get; init; }
    public required string Version { get; init; }
    public required DateTimeOffset CreatedAt { get; init; }
    public required DateTimeOffset UpdatedAt { get; init; }
    public required ApplicationVacancySummary Vacancy { get; init; }
}

public sealed record ApplicationVacancySummary
{
    public required int Id { get; init; }
    public required string Title { get; init; }
    public string? Company { get; init; }
    public string? Url { get; init; }
    public string? Market { get; init; }
    public string? Level { get; init; }
}

public sealed record CreateApplicationRequest(string? CoverLetter);

public sealed record UpdateApplicationStatusRequest(ApplicationStatus Status, string Version);

public enum ApplyResult { Created, AlreadyApplied, VacancyNotFound }

public sealed record ApplyOutcome(ApplyResult Result, ApplicationDto? Application);

public enum StatusChangeResult { Changed, NotFound, IllegalTransition, Conflict }

public sealed record StatusChangeOutcome(StatusChangeResult Result, ApplicationDto? Application);
