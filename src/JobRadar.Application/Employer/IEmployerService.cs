using JobRadar.Application.Applications;
using JobRadar.Application.Vacancies;

namespace JobRadar.Application.Employer;

public interface IEmployerService
{
    Task<VacancyDto> PostVacancyAsync(Guid employerId, CreateVacancyRequest request, CancellationToken ct = default);
    Task<IReadOnlyList<EmployerApplicationDto>> ListApplicationsAsync(Guid employerId, CancellationToken ct = default);
    Task<EmployerStatusChangeOutcome> ChangeApplicationStatusAsync(Guid employerId, int applicationId, UpdateApplicationStatusRequest request, CancellationToken ct = default);
}
