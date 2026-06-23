using JobRadar.Application.SavedFilters;
using JobRadar.Domain.Entities;
using JobRadar.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace JobRadar.Infrastructure.SavedFilters;

/// <summary>
/// CRUD сохранённых фильтров с optimistic concurrency через xmin. На обновлении
/// клиентская Version подставляется как OriginalValue concurrency-токена: если
/// строку успели изменить (xmin сдвинулся), SaveChanges бросает
/// DbUpdateConcurrencyException и мы возвращаем Conflict вместо тихой перезаписи.
/// </summary>
public sealed class SavedFilterService(JobRadarDbContext db, TimeProvider clock) : ISavedFilterService
{
    private const string XminProperty = "xmin";

    public async Task<IReadOnlyList<SavedFilterDto>> ListAsync(Guid userId, CancellationToken ct = default)
    {
        var filters = await db.SavedFilters
            .Where(f => f.UserId == userId)
            .OrderByDescending(f => f.UpdatedAt)
            .ToListAsync(ct);

        return filters.Select(f => ToDto(f, CurrentVersion(f))).ToList();
    }

    public async Task<SavedFilterDto> CreateAsync(Guid userId, CreateSavedFilterRequest request, CancellationToken ct = default)
    {
        var now = clock.GetUtcNow();
        var filter = new SavedFilter
        {
            UserId = userId,
            Name = request.Name.Trim(),
            Market = request.Market,
            Level = request.Level,
            Stack = request.Stack,
            Q = request.Q,
            CreatedAt = now,
            UpdatedAt = now,
        };
        db.SavedFilters.Add(filter);
        await db.SaveChangesAsync(ct);
        return ToDto(filter, CurrentVersion(filter));
    }

    public async Task<SavedFilterUpdateOutcome> UpdateAsync(Guid userId, int id, UpdateSavedFilterRequest request, CancellationToken ct = default)
    {
        var filter = await db.SavedFilters.SingleOrDefaultAsync(f => f.Id == id && f.UserId == userId, ct);
        if (filter is null)
            return new SavedFilterUpdateOutcome(SavedFilterUpdateStatus.NotFound, null);
        if (!uint.TryParse(request.Version, out var clientVersion))
            return new SavedFilterUpdateOutcome(SavedFilterUpdateStatus.Conflict, null);

        filter.Name = request.Name.Trim();
        filter.Market = request.Market;
        filter.Level = request.Level;
        filter.Stack = request.Stack;
        filter.Q = request.Q;
        filter.UpdatedAt = clock.GetUtcNow();

        // Проверяем против версии, которую видел клиент, а не свежезагруженной.
        db.Entry(filter).Property(XminProperty).OriginalValue = clientVersion;

        try
        {
            await db.SaveChangesAsync(ct);
            return new SavedFilterUpdateOutcome(SavedFilterUpdateStatus.Updated, ToDto(filter, CurrentVersion(filter)));
        }
        catch (DbUpdateConcurrencyException)
        {
            return new SavedFilterUpdateOutcome(SavedFilterUpdateStatus.Conflict, null);
        }
    }

    public async Task<bool> DeleteAsync(Guid userId, int id, CancellationToken ct = default)
    {
        var deleted = await db.SavedFilters
            .Where(f => f.Id == id && f.UserId == userId)
            .ExecuteDeleteAsync(ct);
        return deleted > 0;
    }

    private uint CurrentVersion(SavedFilter filter)
        => (uint)db.Entry(filter).Property(XminProperty).CurrentValue!;

    private static SavedFilterDto ToDto(SavedFilter f, uint version) => new()
    {
        Id = f.Id,
        Name = f.Name,
        Market = f.Market,
        Level = f.Level,
        Stack = f.Stack,
        Q = f.Q,
        Version = version.ToString(),
        CreatedAt = f.CreatedAt,
        UpdatedAt = f.UpdatedAt,
    };
}
