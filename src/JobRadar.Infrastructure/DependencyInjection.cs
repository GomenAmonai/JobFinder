using JobRadar.Application.Ingestion;
using JobRadar.Infrastructure.Ingestion;
using JobRadar.Infrastructure.Persistence;
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
        services.TryAddSingleton(TimeProvider.System);
        return services;
    }
}
