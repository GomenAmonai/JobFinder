using JobRadar.Application.SavedFilters;
using JobRadar.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace JobRadar.Infrastructure.SavedFilters;

/// <summary>
/// Совпадение = пустое поле фильтра считается «любым», непустое — точным равенством
/// (market/level/stack). Текстовый Q матчится подстрокой по заголовку (в памяти на
/// уже суженном наборе — спецсимволы пользователя в его же фильтре безопасны).
/// </summary>
public sealed class SavedFilterMatcher(JobRadarDbContext db) : ISavedFilterMatcher
{
    public async Task<IReadOnlyList<Guid>> FindMatchingUserIdsAsync(
        string? market, string? level, string? stack, string title, CancellationToken ct = default)
    {
        var candidates = await db.SavedFilters
            .Where(f => (f.Market == null || f.Market == market)
                     && (f.Level == null || f.Level == level)
                     && (f.Stack == null || f.Stack == stack))
            .Select(f => new { f.UserId, f.Q })
            .ToListAsync(ct);

        var normalizedTitle = (title ?? string.Empty).ToLowerInvariant();
        return candidates
            .Where(c => string.IsNullOrWhiteSpace(c.Q) || normalizedTitle.Contains(c.Q.Trim().ToLowerInvariant()))
            .Select(c => c.UserId)
            .Distinct()
            .ToList();
    }
}
