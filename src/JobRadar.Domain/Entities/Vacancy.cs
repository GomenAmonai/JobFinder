namespace JobRadar.Domain.Entities;

/// <summary>
/// Вакансия из внешнего источника. Уникальна по паре (Source, ExternalId):
/// повторная доставка той же вакансии обновляет запись, а не создаёт дубль.
/// Идемпотентность держится на уникальном индексе + атомарном upsert
/// (VacancyUpsertService) — центральный showpiece. Системный столбец xmin
/// настроен как concurrency-token для интерактивных правок (Phase 3); путь
/// приёма его не задействует (см. JobRadarDbContext).
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

    // Ключ кросс-источниковой дедупликации (нормализованные компания+должность);
    // null, если компании нет. Read API показывает по одной канонической вакансии на ключ.
    public string? DedupKey { get; set; }

    public DateTimeOffset FirstSeen { get; set; }
    public DateTimeOffset LastSeen { get; set; }

    // Автор нативной вакансии: null у агрегированных (с внешних бордов), Id работодателя
    // у запощенных на JobRadar. Определяет, кто двигает статусы откликов (см. EmployerService).
    public Guid? PostedByUserId { get; set; }
}
