namespace JobRadar.Domain.Entities;

/// <summary>
/// Refresh-токен с ротацией. В БД хранится только SHA-256 хеш сырого токена —
/// утечка таблицы не даёт пригодных токенов. На каждый refresh старый помечается
/// RevokedAt и выдаётся новый.
/// </summary>
public class RefreshToken
{
    public Guid Id { get; set; }

    public Guid UserId { get; set; }
    public User? User { get; set; }

    public required string TokenHash { get; set; }

    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset ExpiresAt { get; set; }
    public DateTimeOffset? RevokedAt { get; set; }
}
