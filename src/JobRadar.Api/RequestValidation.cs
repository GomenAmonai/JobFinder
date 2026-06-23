using System.ComponentModel.DataAnnotations;
using JobRadar.Application.Auth;

namespace JobRadar.Api;

/// <summary>
/// Валидация на границе API: возвращает ValidationProblem (400) или null, если ок.
/// В т.ч. ограничивает длину пароля — иначе огромная строка превращает хеширование
/// в DoS-рычаг.
/// </summary>
internal static class RequestValidation
{
    private static readonly EmailAddressAttribute Email = new();
    private const int MaxEmail = 256;
    private const int MinPassword = 8;
    private const int MaxPassword = 128;

    public static IResult? ForRegister(RegisterRequest r)
    {
        var errors = new Dictionary<string, string[]>();
        if (string.IsNullOrWhiteSpace(r.Email) || r.Email.Length > MaxEmail || !Email.IsValid(r.Email))
            errors["email"] = ["A valid email of at most 256 characters is required."];
        if (string.IsNullOrEmpty(r.Password) || r.Password.Length < MinPassword || r.Password.Length > MaxPassword)
            errors["password"] = [$"Password must be {MinPassword}-{MaxPassword} characters."];
        if (r.DisplayName is { Length: > 100 })
            errors["displayName"] = ["Display name must be at most 100 characters."];
        return errors.Count > 0 ? Results.ValidationProblem(errors) : null;
    }

    public static IResult? ForLogin(LoginRequest r)
    {
        var errors = new Dictionary<string, string[]>();
        if (string.IsNullOrWhiteSpace(r.Email) || r.Email.Length > MaxEmail)
            errors["email"] = ["Email is required."];
        if (string.IsNullOrEmpty(r.Password) || r.Password.Length > MaxPassword)
            errors["password"] = ["Password is required."];
        return errors.Count > 0 ? Results.ValidationProblem(errors) : null;
    }

    public static IResult? ForSavedFilter(string name, string? market, string? level, string? stack, string? q)
    {
        var errors = new Dictionary<string, string[]>();
        if (string.IsNullOrWhiteSpace(name) || name.Trim().Length > 100)
            errors["name"] = ["Name is required and must be at most 100 characters."];
        if (market is { Length: > 50 }) errors["market"] = ["At most 50 characters."];
        if (level is { Length: > 30 }) errors["level"] = ["At most 30 characters."];
        if (stack is { Length: > 50 }) errors["stack"] = ["At most 50 characters."];
        if (q is { Length: > 100 }) errors["q"] = ["At most 100 characters."];
        return errors.Count > 0 ? Results.ValidationProblem(errors) : null;
    }
}
