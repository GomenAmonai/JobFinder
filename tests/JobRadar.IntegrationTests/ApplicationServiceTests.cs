using FluentAssertions;
using JobRadar.Application.Applications;
using JobRadar.Domain.Entities;
using JobRadar.Infrastructure.Applications;
using JobRadar.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Testcontainers.PostgreSql;
using Xunit;

namespace JobRadar.IntegrationTests;

public sealed class ApplicationServiceTests : IAsyncLifetime
{
    private readonly PostgreSqlContainer _postgres = new PostgreSqlBuilder("postgres:16").Build();
    private DbContextOptions<JobRadarDbContext> _options = null!;
    private readonly Guid _userId = Guid.NewGuid();
    private readonly Guid _otherUserId = Guid.NewGuid();
    private int _vacancyId;

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
        var vacancy = new Vacancy
        {
            Source = "remotive",
            ExternalId = "v-1",
            Title = "Senior .NET Engineer",
            Company = "Acme",
            FirstSeen = DateTimeOffset.UtcNow,
            LastSeen = DateTimeOffset.UtcNow,
        };
        db.Vacancies.Add(vacancy);
        await db.SaveChangesAsync();
        _vacancyId = vacancy.Id;
    }

    public Task DisposeAsync() => _postgres.DisposeAsync().AsTask();

    private static ApplicationService NewService(JobRadarDbContext db) => new(db, TimeProvider.System);

    [Fact]
    public async Task Apply_creates_a_submitted_application()
    {
        await using var db = new JobRadarDbContext(_options);
        var outcome = await NewService(db).ApplyAsync(_userId, _vacancyId, new CreateApplicationRequest("Hire me"));
        outcome.Application!.Status.Should().Be(ApplicationStatus.Submitted);
    }

    [Fact]
    public async Task Applying_twice_to_the_same_vacancy_is_rejected()
    {
        await using var db = new JobRadarDbContext(_options);
        var service = NewService(db);
        await service.ApplyAsync(_userId, _vacancyId, new CreateApplicationRequest(null));

        await using var db2 = new JobRadarDbContext(_options);
        var outcome = await NewService(db2).ApplyAsync(_userId, _vacancyId, new CreateApplicationRequest(null));

        outcome.Result.Should().Be(ApplyResult.AlreadyApplied);
    }

    [Fact]
    public async Task Applying_to_a_missing_vacancy_returns_not_found()
    {
        await using var db = new JobRadarDbContext(_options);
        var outcome = await NewService(db).ApplyAsync(_userId, 9999, new CreateApplicationRequest(null));
        outcome.Result.Should().Be(ApplyResult.VacancyNotFound);
    }

    [Fact]
    public async Task Applied_vacancy_appears_in_the_user_list()
    {
        await using var db = new JobRadarDbContext(_options);
        var service = NewService(db);
        await service.ApplyAsync(_userId, _vacancyId, new CreateApplicationRequest(null));

        var list = await service.ListAsync(_userId);

        list.Should().ContainSingle(a => a.Vacancy.Id == _vacancyId);
    }

    [Fact]
    public async Task A_user_does_not_see_another_users_applications()
    {
        await using var db = new JobRadarDbContext(_options);
        var service = NewService(db);
        await service.ApplyAsync(_userId, _vacancyId, new CreateApplicationRequest(null));

        var list = await service.ListAsync(_otherUserId);

        list.Should().BeEmpty();
    }

    [Fact]
    public async Task Legal_status_change_succeeds()
    {
        await using var db = new JobRadarDbContext(_options);
        var service = NewService(db);
        var created = await service.ApplyAsync(_userId, _vacancyId, new CreateApplicationRequest(null));

        var outcome = await service.ChangeStatusAsync(_userId, created.Application!.Id,
            new UpdateApplicationStatusRequest(ApplicationStatus.UnderReview, created.Application.Version));

        outcome.Result.Should().Be(StatusChangeResult.Changed);
    }

    [Fact]
    public async Task Illegal_status_change_is_rejected()
    {
        await using var db = new JobRadarDbContext(_options);
        var service = NewService(db);
        var created = await service.ApplyAsync(_userId, _vacancyId, new CreateApplicationRequest(null));
        var rejected = await service.ChangeStatusAsync(_userId, created.Application!.Id,
            new UpdateApplicationStatusRequest(ApplicationStatus.Rejected, created.Application.Version));

        var outcome = await service.ChangeStatusAsync(_userId, created.Application.Id,
            new UpdateApplicationStatusRequest(ApplicationStatus.UnderReview, rejected.Application!.Version));

        outcome.Result.Should().Be(StatusChangeResult.IllegalTransition);
    }

    [Fact]
    public async Task Status_change_with_stale_version_conflicts()
    {
        await using var db1 = new JobRadarDbContext(_options);
        var created = await NewService(db1).ApplyAsync(_userId, _vacancyId, new CreateApplicationRequest(null));

        // Конкурентная смена статуса через отдельный контекст сдвигает xmin.
        await using (var db2 = new JobRadarDbContext(_options))
        {
            await NewService(db2).ChangeStatusAsync(_userId, created.Application!.Id,
                new UpdateApplicationStatusRequest(ApplicationStatus.UnderReview, created.Application.Version));
        }

        await using var db3 = new JobRadarDbContext(_options);
        var outcome = await NewService(db3).ChangeStatusAsync(_userId, created.Application!.Id,
            new UpdateApplicationStatusRequest(ApplicationStatus.InterviewScheduled, created.Application.Version));

        outcome.Result.Should().Be(StatusChangeResult.Conflict);
    }

    [Fact]
    public async Task Delete_removes_the_application()
    {
        await using var db = new JobRadarDbContext(_options);
        var service = NewService(db);
        var created = await service.ApplyAsync(_userId, _vacancyId, new CreateApplicationRequest(null));

        var deleted = await service.DeleteAsync(_userId, created.Application!.Id);

        deleted.Should().BeTrue();
    }
}
