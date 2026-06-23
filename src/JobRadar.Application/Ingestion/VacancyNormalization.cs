namespace JobRadar.Application.Ingestion;

/// <summary>
/// Сведение свободного текста источника к нормализованным граням (рынок, грейд,
/// стек). Портировано из Python-прототипа job_parser.py — единая точка правды,
/// чтобы все коллекторы (C# и Python) классифицировали вакансии одинаково.
/// </summary>
public static class VacancyNormalization
{
    public static string NormalizeMarket(string? text)
    {
        var t = (text ?? string.Empty).ToLowerInvariant();
        if (string.IsNullOrWhiteSpace(t)) return "—";

        bool Has(params string[] keys) => keys.Any(t.Contains);

        if (Has("japan", "tokyo", "osaka", "япони", "токио")) return "Япония";
        if (Has("russia", "росси", "москва", "moscow", "петербург")) return "Россия";
        if (Has("kazakh", "armenia", "georgia", "belarus", "uzbek", "ереван", "алматы")) return "СНГ";
        if (Has("united states", "сша", "u.s.", "new york", "california", "usa")) return "США";
        if (Has("canada", "канада", "toronto")) return "Канада";
        if (Has("europe", "европ", "germany", "berlin", "poland", "spain", "france", "portugal", "netherlands")) return "Европа";
        if (Has("asia", "india", "singapore", "china", "korea", "vietnam")) return "Азия";
        if (Has("worldwide", "anywhere", "global", "remote")) return "Worldwide";
        return "Другое";
    }

    public static string GuessLevel(string? text)
    {
        var t = $" {(text ?? string.Empty).ToLowerInvariant()} ";
        if (Junior.Any(t.Contains)) return "junior";
        if (Senior.Any(t.Contains)) return "senior+";
        if (Middle.Any(t.Contains)) return "middle";
        return "mid/unknown";
    }

    public static string DetectStack(string? text)
        => Dotnet.Any((text ?? string.Empty).ToLowerInvariant().Contains) ? "C#/.NET" : "backend";

    private static readonly string[] Dotnet = [".net", "c#", "csharp", "asp.net", "dotnet"];
    private static readonly string[] Junior = ["junior", "jr.", "intern", "entry", "graduate", "trainee", "младший", "стажёр", "стажер", "джун"];
    private static readonly string[] Senior = ["senior", "lead", "principal", "staff", "architect", "head ", "старший", "ведущий", "тимлид"];
    private static readonly string[] Middle = ["middle", "mid ", "миддл", "средний"];
}
