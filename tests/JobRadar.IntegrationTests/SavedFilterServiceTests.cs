using FluentAssertions;
using JobRadar.Application.SavedFilters;
using JobRadar.Domain.Entities;
using JobRadar.Infrastructure.Persistence;
using JobRadar.Infrastructure.SavedFilters;
using Microsoft.EntityFrameworkCore;
using Testcontainers.PostgreSql;
using Xunit;

namespace JobRadar.IntegrationTests;

public sealed class SavedFilterServiceTests : IAsyncLifetime
{
    private readonly PostgreSqlContainer _postgres = new PostgreSqlBuilder("postgres:16").Build();
    private DbContextOptions<JobRadarDbContext> _options = null!;
    private readonly Guid _userId = Guid.NewGuid();
    private readonly Guid _otherUserId = Guid.NewGuid();

    public async Task InitializeAsync()
    {
        await _postgres.StartAsync();
        _options = new DbContextOptionsBuilder<JobRadarDbContext>()
            .UseNpgsql(_postgres.GetConnectionString())
            .Options;

        await using var db = new JobRadarDbContext(_options);
        await db.Database.MigrateAsync();
        db.Users.AddRange(
            new User { Id = _userId, Email = "owner@x.com", CreatedAt = DateTimeOffset.UtcNow },
            new User { Id = _otherUserId, Email = "other@x.com", CreatedAt = DateTimeOffset.UtcNow });
        await db.SaveChangesAsync();
    }

    public Task DisposeAsync() => _postgres.DisposeAsync().AsTask();

    private static SavedFilterService NewService(JobRadarDbContext db) => new(db, TimeProvider.System);

    [Fact]
    public async Task Create_returns_filter_with_a_version()
    {
        await using var db = new JobRadarDbContext(_options);
        var dto = await NewService(db).CreateAsync(_userId, new CreateSavedFilterRequest("My filter", "Россия", null, null, null));
        dto.Version.Should().NotBeNullOrEmpty();
    }

    [Fact]
    public async Task Created_filter_appears_in_user_list()
    {
        await using var db = new JobRadarDbContext(_options);
        var service = NewService(db);
        await service.CreateAsync(_userId, new CreateSavedFilterRequest("Only one", null, null, null, null));

        var list = await service.ListAsync(_userId);

        list.Should().ContainSingle(f => f.Name == "Only one");
    }

    [Fact]
    public async Task Update_with_current_version_succeeds()
    {
        await using var db = new JobRadarDbContext(_options);
        var service = NewService(db);
        var created = await service.CreateAsync(_userId, new CreateSavedFilterRequest("Original", null, null, null, null));

        var outcome = await service.UpdateAsync(_userId, created.Id,
            new UpdateSavedFilterRequest("Edited", "Япония", null, null, null, created.Version));

        outcome.Status.Should().Be(SavedFilterUpdateStatus.Updated);
    }

    [Fact]
    public async Task Update_with_stale_version_conflicts()
    {
        await using var db1 = new JobRadarDbContext(_options);
        var created = await NewService(db1).CreateAsync(_userId, new CreateSavedFilterRequest("Original", null, null, null, null));

        // Конкурентная правка через отдельный контекст сдвигает xmin.
        await using (var db2 = new JobRadarDbContext(_options))
        {
            await NewService(db2).UpdateAsync(_userId, created.Id,
                new UpdateSavedFilterRequest("Renamed", null, null, null, null, created.Version));
        }

        await using var db3 = new JobRadarDbContext(_options);
        var outcome = await NewService(db3).UpdateAsync(_userId, created.Id,
            new UpdateSavedFilterRequest("Stale write", null, null, null, null, created.Version));

        outcome.Status.Should().Be(SavedFilterUpdateStatus.Conflict);
    }

    [Fact]
    public async Task Update_nonexistent_filter_returns_not_found()
    {
        await using var db = new JobRadarDbContext(_options);
        var outcome = await NewService(db).UpdateAsync(_userId, 9999,
            new UpdateSavedFilterRequest("Nope", null, null, null, null, "1"));
        outcome.Status.Should().Be(SavedFilterUpdateStatus.NotFound);
    }

    [Fact]
    public async Task Delete_removes_the_filter()
    {
        await using var db = new JobRadarDbContext(_options);
        var service = NewService(db);
        var created = await service.CreateAsync(_userId, new CreateSavedFilterRequest("To delete", null, null, null, null));

        var deleted = await service.DeleteAsync(_userId, created.Id);

        deleted.Should().BeTrue();
    }

    [Fact]
    public async Task A_user_cannot_update_another_users_filter()
    {
        await using var db = new JobRadarDbContext(_options);
        var service = NewService(db);
        var created = await service.CreateAsync(_userId, new CreateSavedFilterRequest("Mine", null, null, null, null));

        var outcome = await service.UpdateAsync(_otherUserId, created.Id,
            new UpdateSavedFilterRequest("Hijack", null, null, null, null, created.Version));

        outcome.Status.Should().Be(SavedFilterUpdateStatus.NotFound);
    }
}
