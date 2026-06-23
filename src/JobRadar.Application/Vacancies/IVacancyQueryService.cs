namespace JobRadar.Application.Vacancies;

public interface IVacancyQueryService
{
    Task<PagedResult<VacancyDto>> SearchAsync(VacancyQuery query, CancellationToken ct = default);
}
