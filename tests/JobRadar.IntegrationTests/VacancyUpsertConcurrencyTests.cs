using FluentAssertions;
using JobRadar.Application.Ingestion;
using JobRadar.Infrastructure.Ingestion;
using JobRadar.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Testcontainers.PostgreSql;
using Xunit;

namespace JobRadar.IntegrationTests;

/// <summary>
/// Showpiece-тест: множество потребителей одновременно принимают одну и ту же
/// вакансию. Гарантия — ровно одна строка в БД, без дублей и без падений.
/// </summary>
public sealed class VacancyUpsertConcurrencyTests : IAsyncLifetime
{
    private readonly PostgreSqlContainer _postgres = new PostgreSqlBuilder("postgres:16")
        .Build();

    private DbContextOptions<JobRadarDbContext> _options = null!;

    public async Task InitializeAsync()
    {
        await _postgres.StartAsync();
        _options = new DbContextOptionsBuilder<JobRadarDbContext>()
            .UseNpgsql(_postgres.GetConnectionString())
            .Options;

        await using var db = new JobRadarDbContext(_options);
        await db.Database.MigrateAsync();
    }

    public Task DisposeAsync() => _postgres.DisposeAsync().AsTask();

    [Fact]
    public async Task Concurrent_upserts_of_same_vacancy_produce_a_single_row()
    {
        const int writers = 50;
        var message = new RawVacancyMessage
        {
            Source = "remotive",
            ExternalId = "42",
            Title = "Senior .NET Engineer",
            Company = "Acme",
            Location = "Worldwide",
            Url = "https://example.com/42",
        };

        var results = await Task.WhenAll(Enumerable.Range(0, writers).Select(async _ =>
        {
            await using var db = new JobRadarDbContext(_options);
            var service = new VacancyUpsertService(db, TimeProvider.System);
            return await service.UpsertAsync(VacancyMapper.ToVacancy(message), CancellationToken.None);
        }));

        await using var verify = new JobRadarDbContext(_options);
        var rows = await verify.Vacancies
            .Where(v => v.Source == "remotive" && v.ExternalId == "42")
            .ToListAsync();

        rows.Should().HaveCount(1, "the unique index must collapse all concurrent writers onto one row");
        results.Count(outcome => outcome == UpsertOutcome.Inserted)
            .Should().Be(1, "exactly one writer wins the insert; the rest fall through to update");
    }
}
