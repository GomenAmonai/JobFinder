using JobRadar.Application.Applications;
using JobRadar.Application.Auth;
using JobRadar.Application.Employer;
using JobRadar.Application.Ingestion;
using JobRadar.Application.SavedFilters;
using JobRadar.Application.Vacancies;
using JobRadar.Domain.Entities;
using JobRadar.Infrastructure.Applications;
using JobRadar.Infrastructure.Auth;
using JobRadar.Infrastructure.Employer;
using JobRadar.Infrastructure.Ingestion;
using JobRadar.Infrastructure.Persistence;
using JobRadar.Infrastructure.SavedFilters;
using JobRadar.Infrastructure.Vacancies;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace JobRadar.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, string connectionString)
    {
        services.AddDbContext<JobRadarDbContext>(options => options.UseNpgsql(connectionString));
        services.AddScoped<IVacancyUpsertService, VacancyUpsertService>();
        services.AddScoped<IVacancyQueryService, VacancyQueryService>();
        services.AddScoped<IAuthService, AuthService>();
        services.AddScoped<ISavedFilterService, SavedFilterService>();
        services.AddScoped<ISavedFilterMatcher, SavedFilterMatcher>();
        services.AddScoped<IApplicationService, ApplicationService>();
        services.AddScoped<IEmployerService, EmployerService>();
        services.AddSingleton<IPasswordHasher<User>, PasswordHasher<User>>();
        services.TryAddSingleton(TimeProvider.System);
        return services;
    }
}
