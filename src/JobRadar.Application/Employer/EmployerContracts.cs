using JobRadar.Application.Applications;
using JobRadar.Domain.Entities;

namespace JobRadar.Application.Employer;

public sealed record CreateVacancyRequest(string Title, string? Company, string? Location, string? SalaryRaw, string? Skills, string? Url);

/// <summary>Отклик глазами работодателя: контакт кандидата + статус + версия (xmin) для смены.</summary>
public sealed record EmployerApplicationDto
{
    public required int Id { get; init; }
    public required ApplicationStatus Status { get; init; }
    public string? CoverLetter { get; init; }
    public required string Version { get; init; }
    public required DateTimeOffset CreatedAt { get; init; }
    public required DateTimeOffset UpdatedAt { get; init; }
    public required string CandidateEmail { get; init; }
    public string? CandidateDisplayName { get; init; }
    public required ApplicationVacancySummary Vacancy { get; init; }
}

public sealed record EmployerStatusChangeOutcome(StatusChangeResult Result, ApplicationDto? Application, Guid? CandidateUserId);
