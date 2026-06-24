using System.ComponentModel.DataAnnotations;
using JobRadar.Application.Auth;
using JobRadar.Application.Employer;

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

    public static IResult? ForApplication(string? coverLetter)
        => coverLetter is { Length: > 5000 }
            ? Results.ValidationProblem(new Dictionary<string, string[]> { ["coverLetter"] = ["At most 5000 characters."] })
            : null;

    public static IResult? ForEmployerVacancy(CreateVacancyRequest r)
    {
        var errors = new Dictionary<string, string[]>();
        if (string.IsNullOrWhiteSpace(r.Title) || r.Title.Trim().Length > 500)
            errors["title"] = ["Title is required and must be at most 500 characters."];
        if (r.Company is { Length: > 300 }) errors["company"] = ["At most 300 characters."];
        if (r.Location is { Length: > 200 }) errors["location"] = ["At most 200 characters."];
        if (r.SalaryRaw is { Length: > 100 }) errors["salaryRaw"] = ["At most 100 characters."];
        if (r.Skills is { Length: > 500 }) errors["skills"] = ["At most 500 characters."];
        // Только http(s): иначе сохранённый javascript:-URL стал бы stored XSS при рендере ссылки.
        if (r.Url is { Length: > 0 } url && (url.Length > 1000 || !IsHttpUrl(url)))
            errors["url"] = ["Must be an absolute http(s) URL of at most 1000 characters."];
        return errors.Count > 0 ? Results.ValidationProblem(errors) : null;
    }

    private static bool IsHttpUrl(string value)
        => Uri.TryCreate(value, UriKind.Absolute, out var uri)
            && (uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == Uri.UriSchemeHttps);
}
