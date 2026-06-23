namespace JobRadar.Application.Vacancies;

public sealed record VacancyDto
{
    public required int Id { get; init; }
    public required string Source { get; init; }
    public required string ExternalId { get; init; }
    public required string Title { get; init; }
    public string? Company { get; init; }
    public string? Market { get; init; }
    public string? Level { get; init; }
    public string? Stack { get; init; }
    public string? Location { get; init; }
    public string? SalaryRaw { get; init; }
    public string? Skills { get; init; }
    public string? Url { get; init; }
    public DateTimeOffset? PublishedAt { get; init; }
    public required DateTimeOffset FirstSeen { get; init; }
    public required DateTimeOffset LastSeen { get; init; }
}
