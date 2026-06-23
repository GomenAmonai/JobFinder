namespace JobRadar.Domain.Entities;

/// <summary>
/// Вакансия из внешнего источника. Уникальна по паре (Source, ExternalId):
/// повторная доставка той же вакансии должна обновлять запись, а не создавать
/// дубль. Это гарантируется идемпотентным upsert'ом с optimistic concurrency
/// (системный столбец Postgres xmin) — центральный showpiece проекта.
/// </summary>
public class Vacancy
{
    public int Id { get; set; }

    public required string Source { get; set; }
    public required string ExternalId { get; set; }

    public required string Title { get; set; }
    public string? Company { get; set; }
    public string? Market { get; set; }
    public string? Level { get; set; }
    public string? Stack { get; set; }
    public string? Location { get; set; }

    public string? SalaryRaw { get; set; }
    public long? SalaryMin { get; set; }
    public long? SalaryMax { get; set; }
    public string? SalaryCurrency { get; set; }

    public string? Skills { get; set; }
    public string? Url { get; set; }
    public DateTimeOffset? PublishedAt { get; set; }

    public DateTimeOffset FirstSeen { get; set; }
    public DateTimeOffset LastSeen { get; set; }
}
