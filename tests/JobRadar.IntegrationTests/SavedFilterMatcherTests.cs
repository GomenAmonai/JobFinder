using FluentAssertions;
using JobRadar.Domain.Entities;
using JobRadar.Infrastructure.Persistence;
using JobRadar.Infrastructure.SavedFilters;
using Microsoft.EntityFrameworkCore;
using Testcontainers.PostgreSql;
using Xunit;

namespace JobRadar.IntegrationTests;

public sealed class SavedFilterMatcherTests : IAsyncLifetime
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
    }

    public Task DisposeAsync() => _postgres.DisposeAsync().AsTask();

    private async Task<Guid> SeedUserWithFilter(string? market, string? level, string? stack, string? q)
    {
        var userId = Guid.NewGuid();
        await using var db = new JobRadarDbContext(_options);
        db.Users.Add(new User { Id = userId, Email = $"{userId}@x.com", CreatedAt = DateTimeOffset.UtcNow });
        db.SavedFilters.Add(new SavedFilter
        {
            UserId = userId,
            Name = "f",
            Market = market,
            Level = level,
            Stack = stack,
            Q = q,
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow,
        });
        await db.SaveChangesAsync();
        return userId;
    }

    private async Task<IReadOnlyList<Guid>> Match(string? market, string? level, string? stack, string title)
    {
        await using var db = new JobRadarDbContext(_options);
        return await new SavedFilterMatcher(db).FindMatchingUserIdsAsync(market, level, stack, title);
    }

    [Fact]
    public async Task Matches_filter_with_equal_market()
    {
        var userId = await SeedUserWithFilter("Россия", null, null, null);
        var matched = await Match("Россия", "junior", "backend", "Backend Dev");
        matched.Should().Contain(userId);
    }

    [Fact]
    public async Task Excludes_filter_with_different_stack()
    {
        var userId = await SeedUserWithFilter(null, null, "C#/.NET", null);
        var matched = await Match("Worldwide", "senior+", "backend", "Go Engineer");
        matched.Should().NotContain(userId);
    }

    [Fact]
    public async Task All_null_filter_matches_any_vacancy()
    {
        var userId = await SeedUserWithFilter(null, null, null, null);
        var matched = await Match("Япония", "middle", "C#/.NET", "Anything");
        matched.Should().Contain(userId);
    }

    [Fact]
    public async Task Q_matches_title_substring_case_insensitively()
    {
        var userId = await SeedUserWithFilter(null, null, null, "senior");
        var matched = await Match("Worldwide", "senior+", "backend", "Senior .NET Engineer");
        matched.Should().Contain(userId);
    }

    [Fact]
    public async Task Q_excludes_when_title_does_not_contain_it()
    {
        var userId = await SeedUserWithFilter(null, null, null, "rust");
        var matched = await Match("Worldwide", "junior", "backend", "Junior Go Dev");
        matched.Should().NotContain(userId);
    }
}
