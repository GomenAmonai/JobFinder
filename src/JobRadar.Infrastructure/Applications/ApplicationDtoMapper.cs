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
        Vacancy = new ApplicationVacancySummary
        {
            Id = a.Vacancy!.Id,
            Title = a.Vacancy.Title,
            Company = a.Vacancy.Company,
            Url = a.Vacancy.Url,
            Market = a.Vacancy.Market,
            Level = a.Vacancy.Level,
        },
    };
}
