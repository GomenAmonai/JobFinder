using System.Security.Claims;

namespace JobRadar.Api;

public static class ClaimsPrincipalExtensions
{
    /// <summary>Id текущего пользователя из claim sub (мапится в NameIdentifier).</summary>
    public static Guid? GetUserId(this ClaimsPrincipal user)
    {
        var raw = user.FindFirstValue(ClaimTypes.NameIdentifier) ?? user.FindFirstValue("sub");
        return Guid.TryParse(raw, out var id) ? id : null;
    }
}
