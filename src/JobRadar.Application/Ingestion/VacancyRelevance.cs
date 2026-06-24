namespace JobRadar.Application.Ingestion;

/// <summary>
/// Релевантна ли вакансия профилю JobRadar (.NET/C#/backend/full-stack) по заголовку.
/// Единый фильтр для всех C#-коллекторов, чтобы они не расходились в критериях.
/// </summary>
public static class VacancyRelevance
{
    public static bool IsRelevant(string? title)
    {
        if (string.IsNullOrWhiteSpace(title)) return false;
        var t = title.ToLowerInvariant();
        return Keywords.Any(t.Contains);
    }

    private static readonly string[] Keywords =
        [".net", "c#", "asp.net", "dotnet", "backend", "back-end", "back end",
         "full stack", "full-stack", "fullstack", "golang", "node", "software engineer",
         "software developer", "web developer", "developer"];
}
