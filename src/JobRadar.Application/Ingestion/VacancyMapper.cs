using JobRadar.Domain.Entities;

namespace JobRadar.Application.Ingestion;

/// <summary>
/// Превращает сырое сообщение коллектора в доменную вакансию, проставляя
/// нормализованные рынок/грейд/стек. Метки времени (FirstSeen/LastSeen)
/// выставляет upsert-сервис, а не маппер: они зависят от того, новая это запись
/// или повторная доставка.
/// </summary>
public static class VacancyMapper
{
    public static Vacancy ToVacancy(RawVacancyMessage m)
    {
        var classifyText = $"{m.Title} {m.Skills}";
        return new Vacancy
        {
            Source = m.Source,
            ExternalId = m.ExternalId,
            Title = m.Title,
            Company = m.Company,
            Location = m.Location,
            Market = VacancyNormalization.NormalizeMarket(m.Location),
            Level = VacancyNormalization.GuessLevel(classifyText),
            Stack = VacancyNormalization.DetectStack(classifyText),
            SalaryRaw = m.SalaryRaw,
            Skills = m.Skills,
            Url = m.Url,
            PublishedAt = m.PublishedAt,
        };
    }
}
