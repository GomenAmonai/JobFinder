using System.Text;

namespace JobRadar.Application.Ingestion;

/// <summary>
/// Ключ дедупликации одной и той же вакансии, пришедшей из разных источников
/// (Remotive + RemoteOK и т.д.). Нормализует компанию+должность: lower-case, без
/// пунктуации, схлопнутые пробелы. Без компании ключ не строим (null) — иначе
/// склеили бы неродственные вакансии с пустой компанией.
/// </summary>
public static class DedupKeyBuilder
{
    public static string? Build(string? company, string? title)
    {
        var c = Normalize(company);
        var t = Normalize(title);
        return c.Length == 0 || t.Length == 0 ? null : $"{c}|{t}";
    }

    private static string Normalize(string? text)
    {
        if (string.IsNullOrWhiteSpace(text)) return string.Empty;

        var sb = new StringBuilder(text.Length);
        var lastWasSpace = true; // съедает ведущие пробелы
        foreach (var ch in text.ToLowerInvariant())
        {
            if (char.IsLetterOrDigit(ch))
            {
                sb.Append(ch);
                lastWasSpace = false;
            }
            else if (!lastWasSpace)
            {
                sb.Append(' ');
                lastWasSpace = true;
            }
        }
        return sb.ToString().TrimEnd();
    }
}
