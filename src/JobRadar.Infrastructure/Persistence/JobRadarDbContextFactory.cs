using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace JobRadar.Infrastructure.Persistence;

/// <summary>
/// Design-time фабрика для `dotnet ef`: миграции собираются без запуска хоста и
/// без живой БД. Строку подключения можно переопределить переменной JOBRADAR_DB.
/// </summary>
public sealed class JobRadarDbContextFactory : IDesignTimeDbContextFactory<JobRadarDbContext>
{
    public JobRadarDbContext CreateDbContext(string[] args)
    {
        var connectionString = Environment.GetEnvironmentVariable("JOBRADAR_DB")
            ?? "Host=localhost;Port=5432;Database=jobradar;Username=jobradar;Password=jobradar";

        var options = new DbContextOptionsBuilder<JobRadarDbContext>()
            .UseNpgsql(connectionString)
            .Options;

        return new JobRadarDbContext(options);
    }
}
