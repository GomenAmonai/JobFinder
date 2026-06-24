using JobRadar.Application.Applications;
using JobRadar.Domain.Entities;

namespace JobRadar.Infrastructure.Applications;

/// <summary>Маппинг отклика в DTO. Общий для кандидатского и employer-сервисов,
/// чтобы форма ответа не расходилась. Version — значение xmin (передаёт вызывающий,
/// т.к. читается из ChangeTracker).</summary>
internal static class ApplicationDtoMapper
{
    public static ApplicationDto ToDto(JobApplication a, uint version) => new()
    {
        Id = a.Id,
        Status = a.Status,
        CoverLetter = a.CoverLetter,
        Version = version.ToString(),
        CreatedAt = a.CreatedAt,
        UpdatedAt = a.UpdatedAt,
        Vacancy = ToVacancySummary(a.Vacancy!),
    };

    public static ApplicationVacancySummary ToVacancySummary(Vacancy v) => new()
    {
        Id = v.Id,
        Title = v.Title,
        Company = v.Company,
        Url = v.Url,
        Market = v.Market,
        Level = v.Level,
    };
}
