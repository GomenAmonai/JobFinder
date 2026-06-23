namespace JobRadar.Application.Vacancies;

/// <summary>Фильтры и пагинация для чтения вакансий. Все поля опциональны.</summary>
public sealed record VacancyQuery
{
    public string? Market { get; init; }
    public string? Level { get; init; }
    public string? Stack { get; init; }
    public string? Q { get; init; }
    public int Page { get; init; } = 1;
    public int PageSize { get; init; } = 20;
}
