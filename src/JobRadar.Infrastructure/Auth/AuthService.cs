using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using JobRadar.Application.Auth;
using JobRadar.Domain.Entities;
using JobRadar.Infrastructure.Persistence;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using Npgsql;

namespace JobRadar.Infrastructure.Auth;

/// <summary>
/// Регистрация/логин/refresh на собственных JWT (без полного ASP.NET Identity).
/// Пароли — через <see cref="IPasswordHasher{TUser}"/>; refresh-токены опаковые,
/// в БД хранится только их SHA-256 хеш, и они ротируются при каждом обновлении.
/// </summary>
public sealed class AuthService(
    JobRadarDbContext db,
    IPasswordHasher<User> passwordHasher,
    IOptions<JwtSettings> jwtOptions,
    TimeProvider clock) : IAuthService
{
    private readonly JwtSettings _jwt = jwtOptions.Value;

    public async Task<AuthOutcome> RegisterAsync(RegisterRequest request, CancellationToken ct = default)
    {
        var email = Normalize(request.Email);
        if (await db.Users.AnyAsync(u => u.Email == email, ct))
            return AuthOutcome.Fail(AuthError.EmailAlreadyUsed);

        var user = new User
        {
            Id = Guid.NewGuid(),
            Email = email,
            DisplayName = string.IsNullOrWhiteSpace(request.DisplayName) ? null : request.DisplayName.Trim(),
            // Роль берётся из запроса осознанно: self-service регистрация работодателя без
            // верификации (демо). Если у Employer появятся привилегии вне своих вакансий
            // (биллинг, модерация) — роль нужно будет выдавать через отдельный проверенный путь.
            Role = request.Role,
            CreatedAt = clock.GetUtcNow(),
        };
        user.PasswordHash = passwordHasher.HashPassword(user, request.Password);
        db.Users.Add(user);

        try
        {
            await db.SaveChangesAsync(ct);
        }
        catch (DbUpdateException ex) when (IsUniqueViolation(ex))
        {
            // Конкурентная регистрация того же email опередила — уникальный индекс отклонил.
            return AuthOutcome.Fail(AuthError.EmailAlreadyUsed);
        }

        return AuthOutcome.Success(await IssueTokensAsync(user, ct));
    }

    public async Task<AuthOutcome> LoginAsync(LoginRequest request, CancellationToken ct = default)
    {
        var email = Normalize(request.Email);
        var user = await db.Users.SingleOrDefaultAsync(u => u.Email == email, ct);
        if (user is null)
            return AuthOutcome.Fail(AuthError.InvalidCredentials);

        var verification = passwordHasher.VerifyHashedPassword(user, user.PasswordHash, request.Password);
        if (verification == PasswordVerificationResult.Failed)
            return AuthOutcome.Fail(AuthError.InvalidCredentials);

        return AuthOutcome.Success(await IssueTokensAsync(user, ct));
    }

    public async Task<AuthOutcome> RefreshAsync(string refreshToken, CancellationToken ct = default)
    {
        var hash = HashToken(refreshToken);
        var now = clock.GetUtcNow();

        var stored = await db.RefreshTokens
            .Include(t => t.User)
            .SingleOrDefaultAsync(t => t.TokenHash == hash, ct);

        if (stored?.User is null || stored.ExpiresAt <= now)
            return AuthOutcome.Fail(AuthError.InvalidRefreshToken);

        if (stored.RevokedAt is not null)
        {
            // Повторное предъявление уже отротированного токена = вероятная кража (RFC 9700):
            // гасим всю живую цепочку пользователя, а не только этот токен.
            await db.RefreshTokens
                .Where(t => t.UserId == stored.UserId && t.RevokedAt == null)
                .ExecuteUpdateAsync(s => s.SetProperty(t => t.RevokedAt, now), ct);
            return AuthOutcome.Fail(AuthError.InvalidRefreshToken);
        }

        stored.RevokedAt = now; // ротация: гасим использованный токен и выдаём новую пару
        return AuthOutcome.Success(await IssueTokensAsync(stored.User, ct));
    }

    private async Task<TokenPair> IssueTokensAsync(User user, CancellationToken ct)
    {
        var now = clock.GetUtcNow();
        var accessExpires = now.AddMinutes(_jwt.AccessTokenMinutes);

        // sub → ClaimTypes.NameIdentifier по дефолтному inbound-маппингу JwtBearer; на этом
        // держится и SignalR Clients.User(userId), и GetUserId(). Role → ClaimTypes.Role для RequireRole.
        // Если когда-то выключим MapInboundClaims — нужен явный IUserIdProvider по sub.
        var claims = new[]
        {
            new Claim(JwtRegisteredClaimNames.Sub, user.Id.ToString()),
            new Claim(JwtRegisteredClaimNames.Email, user.Email),
            new Claim(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString()),
            new Claim("name", user.DisplayName ?? user.Email),
            new Claim(ClaimTypes.Role, user.Role.ToString()),
        };

        var credentials = new SigningCredentials(
            new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_jwt.SigningKey)),
            SecurityAlgorithms.HmacSha256);

        var jwt = new JwtSecurityToken(
            issuer: _jwt.Issuer,
            audience: _jwt.Audience,
            claims: claims,
            notBefore: now.UtcDateTime,
            expires: accessExpires.UtcDateTime,
            signingCredentials: credentials);

        var accessToken = new JwtSecurityTokenHandler().WriteToken(jwt);

        var rawRefresh = Convert.ToHexString(RandomNumberGenerator.GetBytes(32));
        db.RefreshTokens.Add(new RefreshToken
        {
            Id = Guid.NewGuid(),
            UserId = user.Id,
            TokenHash = HashToken(rawRefresh),
            CreatedAt = now,
            ExpiresAt = now.AddDays(_jwt.RefreshTokenDays),
        });
        await db.SaveChangesAsync(ct);

        return new TokenPair(accessToken, rawRefresh, accessExpires);
    }

    private static string Normalize(string email) => email.Trim().ToLowerInvariant();

    private static string HashToken(string token)
        => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(token)));

    private static bool IsUniqueViolation(DbUpdateException ex)
        => ex.InnerException is PostgresException { SqlState: PostgresErrorCodes.UniqueViolation };
}
