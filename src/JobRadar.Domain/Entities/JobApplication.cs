namespace JobRadar.Domain.Entities;

/// <summary>
/// Отклик кандидата на вакансию. Сейчас это персональный трекер: кандидат сам
/// ведёт свой пайплайн по агрегированным вакансиям (у внешних работодателей нет
/// аккаунта на JobRadar, доставлять отклик пока некому). Уникален по паре
/// (UserId, VacancyId) — дважды откликнуться нельзя. Статус правится интерактивно,
/// поэтому под optimistic concurrency (системный столбец xmin).
/// Имя JobApplication, а не Application — иначе тип сталкивается с именем слоя
/// JobRadar.Application (он достижим как голый токен под корнем JobRadar).
/// </summary>
public class JobApplication
{
    public int Id { get; set; }

    public Guid UserId { get; set; }

    public int VacancyId { get; set; }
    public Vacancy? Vacancy { get; set; }

    public ApplicationStatus Status { get; set; }
    public string? CoverLetter { get; set; }

    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }
}
