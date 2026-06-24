namespace JobRadar.Domain.Entities;

/// <summary>
/// Роль пользователя. Candidate откликается и ведёт свой пайплайн; Employer постит
/// нативные вакансии на JobRadar и управляет статусами входящих откликов.
/// </summary>
public enum UserRole
{
    Candidate,
    Employer,
}
