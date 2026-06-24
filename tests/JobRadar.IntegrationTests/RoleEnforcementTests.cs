using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using JobRadar.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Testcontainers.PostgreSql;
using Xunit;

namespace JobRadar.IntegrationTests;

public sealed class RoleEnforcementTests : IAsyncLifetime
{
    private readonly PostgreSqlContainer _postgres = new PostgreSqlBuilder("postgres:16").Build();
    private JobRadarApiFactory _factory = null!;

    public async Task InitializeAsync()
    {
        await _postgres.StartAsync();
        var connectionString = _postgres.GetConnectionString();
        await using (var db = new JobRadarDbContext(
            new DbContextOptionsBuilder<JobRadarDbContext>().UseNpgsql(connectionString).Options))
            await db.Database.MigrateAsync();
        _factory = new JobRadarApiFactory(connectionString);
    }

    public async Task DisposeAsync()
    {
        await _factory.DisposeAsync();
        await _postgres.DisposeAsync();
    }

    [Fact]
    public async Task Anonymous_request_to_employer_endpoint_is_unauthorized()
    {
        var response = await _factory.CreateClient().GetAsync("/employer/applications");
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Candidate_is_forbidden_from_employer_endpoint()
    {
        var client = _factory.CreateClient();
        await AuthorizeAsync(client, "candidate@x.com", "Candidate");

        var response = await client.GetAsync("/employer/applications");

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task Employer_is_allowed_on_employer_endpoint()
    {
        var client = _factory.CreateClient();
        await AuthorizeAsync(client, "employer@x.com", "Employer");

        var response = await client.GetAsync("/employer/applications");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    private static async Task AuthorizeAsync(HttpClient client, string email, string role)
    {
        var registration = await client.PostAsJsonAsync("/auth/register", new { email, password = "P@ssw0rd!", role });
        if (!registration.IsSuccessStatusCode)
            throw new Xunit.Sdk.XunitException($"register {(int)registration.StatusCode}: {await registration.Content.ReadAsStringAsync()}");
        var tokens = await registration.Content.ReadFromJsonAsync<TokenResponse>();
        client.DefaultRequestHeaders.Authorization = new("Bearer", tokens!.AccessToken);
    }

    private sealed record TokenResponse(string AccessToken, string RefreshToken, DateTimeOffset AccessTokenExpiresAt);
}
