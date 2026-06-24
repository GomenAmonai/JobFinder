namespace JobRadar.Application.SavedFilters;

public interface ISavedFilterService
{
    Task<IReadOnlyList<SavedFilterDto>> ListAsync(Guid userId, CancellationToken ct = default);
    Task<SavedFilterDto> CreateAsync(Guid userId, CreateSavedFilterRequest request, CancellationToken ct = default);
    Task<SavedFilterUpdateOutcome> UpdateAsync(Guid userId, int id, UpdateSavedFilterRequest request, CancellationToken ct = default);
    Task<bool> DeleteAsync(Guid userId, int id, CancellationToken ct = default);
}
