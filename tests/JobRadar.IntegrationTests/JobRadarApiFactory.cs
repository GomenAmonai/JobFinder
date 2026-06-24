using JobRadar.Infrastructure.Persistence;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace JobRadar.IntegrationTests;

/// <summary>
/// Поднимает реальное API в памяти (TestServer) поверх контейнерного Postgres —
/// чтобы прогонять цепочку JWT → claim роли → RequireRole по-настоящему, а не в обход HTTP.
/// DbContext подменяется на тестовое подключение в ConfigureTestServices (после
/// регистраций приложения), т.к. строку приложение читает на этапе сборки билдера.
/// </summary>
public sealed class JobRadarApiFactory(string connectionString) : WebApplicationFactory<Program>
{
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Development");
        builder.ConfigureTestServices(services =>
        {
            var descriptor = services.SingleOrDefault(d => d.ServiceType == typeof(DbContextOptions<JobRadarDbContext>));
            if (descriptor is not null) services.Remove(descriptor);
            services.AddDbContext<JobRadarDbContext>(options => options.UseNpgsql(connectionString));
        });
    }
}
