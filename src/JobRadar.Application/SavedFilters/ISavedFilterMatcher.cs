namespace JobRadar.Application.SavedFilters;

/// <summary>
/// Находит пользователей, чьи сохранённые фильтры совпадают с изменившейся
/// вакансией — для таргетированного SignalR-push «новая вакансия под твой фильтр».
/// </summary>
public interface ISavedFilterMatcher
{
    Task<IReadOnlyList<Guid>> FindMatchingUserIdsAsync(
        string? market, string? level, string? stack, string title, CancellationToken ct = default);
}
