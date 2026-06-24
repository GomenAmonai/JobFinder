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
        var salary = SalaryParser.Parse(m.SalaryRaw);
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
            SalaryMin = salary.Min,
            SalaryMax = salary.Max,
            SalaryCurrency = salary.Currency,
            Skills = m.Skills,
            Url = m.Url,
            // Postgres timestamptz принимает только UTC (offset 0); источники часто
            // отдают дату с локальным смещением — приводим к UTC на границе нормализации.
            PublishedAt = m.PublishedAt?.ToUniversalTime(),
            DedupKey = DedupKeyBuilder.Build(m.Company, m.Title),
        };
    }
}
