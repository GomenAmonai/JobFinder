using JobRadar.Domain.Entities;

namespace JobRadar.Application.Auth;

public sealed record RegisterRequest(string Email, string Password, string? DisplayName, UserRole Role = UserRole.Candidate);

public sealed record LoginRequest(string Email, string Password);

public sealed record RefreshRequest(string RefreshToken);

public sealed record TokenPair(string AccessToken, string RefreshToken, DateTimeOffset AccessTokenExpiresAt);

public enum AuthError { None, EmailAlreadyUsed, InvalidCredentials, InvalidRefreshToken }

/// <summary>Результат операции auth: либо токены, либо причина отказа (без исключений в потоке управления).</summary>
public sealed record AuthOutcome(AuthError Error, TokenPair? Tokens)
{
    public bool Succeeded => Error == AuthError.None;

    public static AuthOutcome Success(TokenPair tokens) => new(AuthError.None, tokens);
    public static AuthOutcome Fail(AuthError error) => new(error, null);
}
