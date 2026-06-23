using FluentAssertions;
using JobRadar.Application.Auth;
using JobRadar.Domain.Entities;
using JobRadar.Infrastructure.Auth;
using JobRadar.Infrastructure.Persistence;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Testcontainers.PostgreSql;
using Xunit;

namespace JobRadar.IntegrationTests;

public sealed class AuthServiceTests : IAsyncLifetime
{
    private readonly PostgreSqlContainer _postgres = new PostgreSqlBuilder("postgres:16").Build();
    private DbContextOptions<JobRadarDbContext> _options = null!;

    private static readonly JwtSettings Jwt = new()
    {
        Issuer = "test",
        Audience = "test",
        SigningKey = "test-signing-key-at-least-32-bytes-long-0123456789",
        AccessTokenMinutes = 15,
        RefreshTokenDays = 7,
    };

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

    private AuthService NewService(JobRadarDbContext db)
        => new(db, new PasswordHasher<User>(), Options.Create(Jwt), TimeProvider.System);

    [Fact]
    public async Task Register_returns_tokens_for_new_email()
    {
        await using var db = new JobRadarDbContext(_options);
        var outcome = await NewService(db).RegisterAsync(new RegisterRequest("new@x.com", "P@ssw0rd!", "New"));
        outcome.Tokens.Should().NotBeNull();
    }

    [Fact]
    public async Task Register_rejects_duplicate_email()
    {
        await using var db = new JobRadarDbContext(_options);
        var service = NewService(db);
        await service.RegisterAsync(new RegisterRequest("dup@x.com", "P@ssw0rd!", null));

        var second = await service.RegisterAsync(new RegisterRequest("DUP@x.com", "Other1!", null));

        second.Error.Should().Be(AuthError.EmailAlreadyUsed);
    }

    [Fact]
    public async Task Login_with_correct_password_succeeds()
    {
        await using var db = new JobRadarDbContext(_options);
        var service = NewService(db);
        await service.RegisterAsync(new RegisterRequest("login@x.com", "P@ssw0rd!", null));

        var outcome = await service.LoginAsync(new LoginRequest("login@x.com", "P@ssw0rd!"));

        outcome.Succeeded.Should().BeTrue();
    }

    [Fact]
    public async Task Login_with_wrong_password_fails()
    {
        await using var db = new JobRadarDbContext(_options);
        var service = NewService(db);
        await service.RegisterAsync(new RegisterRequest("badpass@x.com", "P@ssw0rd!", null));

        var outcome = await service.LoginAsync(new LoginRequest("badpass@x.com", "wrong"));

        outcome.Error.Should().Be(AuthError.InvalidCredentials);
    }

    [Fact]
    public async Task Refresh_with_valid_token_succeeds()
    {
        await using var db = new JobRadarDbContext(_options);
        var service = NewService(db);
        var registered = await service.RegisterAsync(new RegisterRequest("refresh@x.com", "P@ssw0rd!", null));

        var refreshed = await service.RefreshAsync(registered.Tokens!.RefreshToken);

        refreshed.Succeeded.Should().BeTrue();
    }

    [Fact]
    public async Task Rotated_refresh_token_cannot_be_reused()
    {
        await using var db = new JobRadarDbContext(_options);
        var service = NewService(db);
        var registered = await service.RegisterAsync(new RegisterRequest("rotate@x.com", "P@ssw0rd!", null));
        var oldToken = registered.Tokens!.RefreshToken;
        await service.RefreshAsync(oldToken);

        var reused = await service.RefreshAsync(oldToken);

        reused.Error.Should().Be(AuthError.InvalidRefreshToken);
    }

    [Fact]
    public async Task Refresh_with_unknown_token_fails()
    {
        await using var db = new JobRadarDbContext(_options);
        var reused = await NewService(db).RefreshAsync("deadbeef");
        reused.Error.Should().Be(AuthError.InvalidRefreshToken);
    }
}
