namespace JobRadar.Domain.Entities;

/// <summary>Зарегистрированный пользователь. Email уникален (нормализуется в lower-case).</summary>
public class User
{
    public Guid Id { get; set; }

    public required string Email { get; set; }
    public string PasswordHash { get; set; } = string.Empty;
    public string? DisplayName { get; set; }
    public UserRole Role { get; set; }

    public DateTimeOffset CreatedAt { get; set; }
}
