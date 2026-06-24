using FluentAssertions;
using JobRadar.Application.Applications;
using JobRadar.Application.Employer;
using JobRadar.Application.Ingestion;
using JobRadar.Domain.Entities;
using JobRadar.Infrastructure.Applications;
using JobRadar.Infrastructure.Employer;
using JobRadar.Infrastructure.Persistence;
using JobRadar.Infrastructure.Vacancies;
using Microsoft.EntityFrameworkCore;
using Testcontainers.PostgreSql;
using Xunit;

namespace JobRadar.IntegrationTests;

public sealed class EmployerServiceTests : IAsyncLifetime
{
    private readonly PostgreSqlContainer _postgres = new PostgreSqlBuilder("postgres:16").Build();
    private DbContextOptions<JobRadarDbContext> _options = null!;
    private readonly Guid _employerId = Guid.NewGuid();
    private readonly Guid _otherEmployerId = Guid.NewGuid();
    private readonly Guid _candidateId = Guid.NewGuid();

    public async Task InitializeAsync()
    {
        await _postgres.StartAsync();
        _options = new DbContextOptionsBuilder<JobRadarDbContext>()
            .UseNpgsql(_postgres.GetConnectionString())
            .Options;

        await using var db = new JobRadarDbContext(_options);
        await db.Database.MigrateAsync();
        db.Users.AddRange(
            new User { Id = _employerId, Email = "employer@x.com", Role = UserRole.Employer, CreatedAt = DateTimeOffset.UtcNow },
            new User { Id = _otherEmployerId, Email = "other@x.com", Role = UserRole.Employer, CreatedAt = DateTimeOffset.UtcNow },
            new User { Id = _candidateId, Email = "candidate@x.com", Role = UserRole.Candidate, CreatedAt = DateTimeOffset.UtcNow });
        await db.SaveChangesAsync();
    }

    public Task DisposeAsync() => _postgres.DisposeAsync().AsTask();

    private static EmployerService NewEmployer(JobRadarDbContext db) => new(db, TimeProvider.System);
    private static ApplicationService NewApplications(JobRadarDbContext db) => new(db, TimeProvider.System);
    private static readonly CreateVacancyRequest Posting =
        new("Senior .NET Engineer", "Acme", "Worldwide", "$90k - $120k", "C#, ASP.NET", null);

    [Fact]
    public async Task Post_creates_a_native_vacancy()
    {
        await using var db = new JobRadarDbContext(_options);
        var created = await NewEmployer(db).PostVacancyAsync(_employerId, Posting);
        created.Source.Should().Be("JobRadar");
    }

    [Fact]
    public async Task Posted_vacancy_is_visible_in_the_public_feed()
    {
        int vacancyId;
        await using (var db = new JobRadarDbContext(_options))
            vacancyId = (await NewEmployer(db).PostVacancyAsync(_employerId, Posting)).Id;

        await using var read = new JobRadarDbContext(_options);
        var feed = await new VacancyQueryService(read).SearchAsync(new() { Q = "Senior .NET" });

        feed.Items.Should().ContainSingle(v => v.Id == vacancyId);
    }

    [Fact]
    public async Task Native_vacancy_is_not_collapsed_by_a_matching_aggregated_one()
    {
        // Агрегированная вакансия с тем же company|title и меньшим Id — без фикса она
        // стала бы канонической и спрятала нативную из ленты.
        await using (var seed = new JobRadarDbContext(_options))
        {
            seed.Vacancies.Add(new Vacancy
            {
                Source = "remotive",
                ExternalId = "agg-1",
                Title = Posting.Title,
                Company = Posting.Company,
                DedupKey = DedupKeyBuilder.Build(Posting.Company, Posting.Title),
                FirstSeen = DateTimeOffset.UtcNow,
                LastSeen = DateTimeOffset.UtcNow,
            });
            await seed.SaveChangesAsync();
        }

        int nativeId;
        await using (var db = new JobRadarDbContext(_options))
            nativeId = (await NewEmployer(db).PostVacancyAsync(_employerId, Posting)).Id;

        await using var read = new JobRadarDbContext(_options);
        var feed = await new VacancyQueryService(read).SearchAsync(new() { Q = "Senior .NET" });

        feed.Items.Should().Contain(v => v.Id == nativeId);
    }

    [Fact]
    public async Task Employer_sees_applications_to_their_vacancies()
    {
        var vacancyId = await PostAndApplyAsync();

        await using var db = new JobRadarDbContext(_options);
        var apps = await NewEmployer(db).ListApplicationsAsync(_employerId);

        apps.Should().ContainSingle(a => a.CandidateEmail == "candidate@x.com");
    }

    [Fact]
    public async Task Employer_does_not_see_other_employers_applications()
    {
        await PostAndApplyAsync();

        await using var db = new JobRadarDbContext(_options);
        var apps = await NewEmployer(db).ListApplicationsAsync(_otherEmployerId);

        apps.Should().BeEmpty();
    }

    [Fact]
    public async Task Owning_employer_can_advance_status()
    {
        var (appId, version) = await PostApplyAndGetAsync();

        await using var db = new JobRadarDbContext(_options);
        var outcome = await NewEmployer(db).ChangeApplicationStatusAsync(_employerId, appId,
            new UpdateApplicationStatusRequest(ApplicationStatus.UnderReview, version));

        outcome.Result.Should().Be(StatusChangeResult.Changed);
    }

    [Fact]
    public async Task Non_owning_employer_cannot_change_status()
    {
        var (appId, version) = await PostApplyAndGetAsync();

        await using var db = new JobRadarDbContext(_options);
        var outcome = await NewEmployer(db).ChangeApplicationStatusAsync(_otherEmployerId, appId,
            new UpdateApplicationStatusRequest(ApplicationStatus.UnderReview, version));

        outcome.Result.Should().Be(StatusChangeResult.NotFound);
    }

    [Fact]
    public async Task Employer_cannot_withdraw_an_application()
    {
        var (appId, version) = await PostApplyAndGetAsync();

        await using var db = new JobRadarDbContext(_options);
        var outcome = await NewEmployer(db).ChangeApplicationStatusAsync(_employerId, appId,
            new UpdateApplicationStatusRequest(ApplicationStatus.Withdrawn, version));

        outcome.Result.Should().Be(StatusChangeResult.IllegalTransition);
    }

    [Fact]
    public async Task Status_change_notifies_the_candidate_user()
    {
        var (appId, version) = await PostApplyAndGetAsync();

        await using var db = new JobRadarDbContext(_options);
        var outcome = await NewEmployer(db).ChangeApplicationStatusAsync(_employerId, appId,
            new UpdateApplicationStatusRequest(ApplicationStatus.UnderReview, version));

        outcome.CandidateUserId.Should().Be(_candidateId);
    }

    [Fact]
    public async Task Candidate_on_a_native_vacancy_can_only_withdraw()
    {
        var (appId, version) = await PostApplyAndGetAsync();

        await using var db = new JobRadarDbContext(_options);
        var outcome = await NewApplications(db).ChangeStatusAsync(_candidateId, appId,
            new UpdateApplicationStatusRequest(ApplicationStatus.UnderReview, version));

        outcome.Result.Should().Be(StatusChangeResult.IllegalTransition);
    }

    [Fact]
    public async Task Candidate_can_withdraw_a_native_application()
    {
        var (appId, version) = await PostApplyAndGetAsync();

        await using var db = new JobRadarDbContext(_options);
        var outcome = await NewApplications(db).ChangeStatusAsync(_candidateId, appId,
            new UpdateApplicationStatusRequest(ApplicationStatus.Withdrawn, version));

        outcome.Result.Should().Be(StatusChangeResult.Changed);
    }

    private async Task<int> PostAndApplyAsync()
    {
        await using var db = new JobRadarDbContext(_options);
        var vacancy = await NewEmployer(db).PostVacancyAsync(_employerId, Posting);
        await NewApplications(db).ApplyAsync(_candidateId, vacancy.Id, new CreateApplicationRequest("Interested"));
        return vacancy.Id;
    }

    private async Task<(int AppId, string Version)> PostApplyAndGetAsync()
    {
        await using var db = new JobRadarDbContext(_options);
        var vacancy = await NewEmployer(db).PostVacancyAsync(_employerId, Posting);
        var applied = await NewApplications(db).ApplyAsync(_candidateId, vacancy.Id, new CreateApplicationRequest("Interested"));
        return (applied.Application!.Id, applied.Application.Version);
    }
}
