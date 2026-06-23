namespace JobRadar.Domain.Entities;

/// <summary>
/// Сохранённый пользователем поисковый фильтр. Правится интерактивно, поэтому
/// под optimistic concurrency (системный столбец xmin) — конкурентная правка
/// даёт конфликт вместо тихой потери изменений.
/// </summary>
public class SavedFilter
{
    public int Id { get; set; }

    public Guid UserId { get; set; }

    public required string Name { get; set; }
    public string? Market { get; set; }
    public string? Level { get; set; }
    public string? Stack { get; set; }
    public string? Q { get; set; }

    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }
}
