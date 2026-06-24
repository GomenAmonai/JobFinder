using FluentAssertions;
using JobRadar.Application.Vacancies;
using JobRadar.Domain.Entities;
using JobRadar.Infrastructure.Persistence;
using JobRadar.Infrastructure.Vacancies;
using Microsoft.EntityFrameworkCore;
using Testcontainers.PostgreSql;
using Xunit;

namespace JobRadar.IntegrationTests;

public sealed class VacancyQueryServiceTests : IAsyncLifetime
{
    private readonly PostgreSqlContainer _postgres = new PostgreSqlBuilder("postgres:16").Build();
    private DbContextOptions<JobRadarDbContext> _options = null!;

    public async Task InitializeAsync()
    {
        await _postgres.StartAsync();
        _options = new DbContextOptionsBuilder<JobRadarDbContext>()
            .UseNpgsql(_postgres.GetConnectionString())
            .Options;

        await using var db = new JobRadarDbContext(_options);
        await db.Database.MigrateAsync();
        db.Vacancies.AddRange(
            Make("1", "Россия", "junior", "C#/.NET", "Junior .NET Developer", Day(20)),
            Make("2", "Япония", "senior+", "backend", "Senior Go Engineer", Day(22)),
            Make("3", "Россия", "middle", "C#/.NET", "Middle Backend (C#)", Day(21)));
        await db.SaveChangesAsync();
    }

    public Task DisposeAsync() => _postgres.DisposeAsync().AsTask();

    [Fact]
    public async Task Filters_by_market()
    {
        var result = await Search(new VacancyQuery { Market = "Россия" });
        result.Total.Should().Be(2);
    }

    [Fact]
    public async Task Orders_by_published_date_descending()
    {
        var result = await Search(new VacancyQuery());
        result.Items[0].ExternalId.Should().Be("2");
    }

    [Fact]
    public async Task Free_text_search_matches_title_case_insensitively()
    {
        var result = await Search(new VacancyQuery { Q = "go engineer" });
        result.Items.Should().ContainSingle(v => v.ExternalId == "2");
    }

    [Fact]
    public async Task Free_text_search_treats_wildcards_literally()
    {
        // Без экранирования "%" дал бы LIKE '%%%' и вернул всё; экранированный — ничего.
        var result = await Search(new VacancyQuery { Q = "%" });
        result.Total.Should().Be(0);
    }

    [Fact]
    public async Task Paginates_and_reports_total_pages()
    {
        var result = await Search(new VacancyQuery { Page = 1, PageSize = 2 });
        result.TotalPages.Should().Be(2);
    }

    [Fact]
    public async Task Collapses_cross_source_duplicates_to_one()
    {
        await using (var seed = new JobRadarDbContext(_options))
        {
            seed.Vacancies.AddRange(
                MakeWithKey("r1", "remotive", "Dedup Target Engineer", "acme|dedup target engineer"),
                MakeWithKey("o1", "remoteok", "Dedup Target Engineer", "acme|dedup target engineer"));
            await seed.SaveChangesAsync();
        }

        var result = await Search(new VacancyQuery { Q = "Dedup Target" });

        result.Total.Should().Be(1);
    }

    [Fact]
    public async Task Dedup_keeps_the_duplicate_that_matches_the_filtered_facet()
    {
        await using (var seed = new JobRadarDbContext(_options))
        {
            // Первый по Id — без рынка; дубль с тем же ключом несёт Market="Европа".
            seed.Vacancies.Add(MakeWithKey("first", "remotive", "Faceted Role", "acme|faceted role"));
            var withMarket = MakeWithKey("second", "remoteok", "Faceted Role", "acme|faceted role");
            withMarket.Market = "Европа";
            seed.Vacancies.Add(withMarket);
            await seed.SaveChangesAsync();
        }

        var result = await Search(new VacancyQuery { Market = "Европа" });

        result.Items.Should().ContainSingle(v => v.ExternalId == "second");
    }

    private async Task<PagedResult<VacancyDto>> Search(VacancyQuery query)
    {
        await using var db = new JobRadarDbContext(_options);
        return await new VacancyQueryService(db).SearchAsync(query);
    }

    private static DateTimeOffset Day(int day) => new(2026, 6, day, 0, 0, 0, TimeSpan.Zero);

    private static Vacancy Make(string id, string market, string level, string stack, string title, DateTimeOffset published)
        => new()
        {
            Source = "remotive",
            ExternalId = id,
            Title = title,
            Market = market,
            Level = level,
            Stack = stack,
            PublishedAt = published,
            FirstSeen = published,
            LastSeen = published,
        };

    private static Vacancy MakeWithKey(string id, string source, string title, string dedupKey)
        => new()
        {
            Source = source,
            ExternalId = id,
            Title = title,
            DedupKey = dedupKey,
            FirstSeen = Day(20),
            LastSeen = Day(20),
        };
}
