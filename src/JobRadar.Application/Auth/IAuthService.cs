namespace JobRadar.Application.Auth;

public interface IAuthService
{
    Task<AuthOutcome> RegisterAsync(RegisterRequest request, CancellationToken ct = default);
    Task<AuthOutcome> LoginAsync(LoginRequest request, CancellationToken ct = default);
    Task<AuthOutcome> RefreshAsync(string refreshToken, CancellationToken ct = default);
}
