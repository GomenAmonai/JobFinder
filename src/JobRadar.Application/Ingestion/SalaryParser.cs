using System.Globalization;
using System.Text.RegularExpressions;

namespace JobRadar.Application.Ingestion;

/// <summary>
/// Извлекает структурную зарплату (мин/макс/валюта) из свободной строки источника
/// (`SalaryRaw`). Источники отдают её как попало («$70k - $90k», «50,000 EUR»,
/// «100000»), поэтому парсинг best-effort: что не распозналось — остаётся null,
/// а `SalaryRaw` всегда сохраняется как есть. Единая точка для всех коллекторов.
/// </summary>
public static partial class SalaryParser
{
    // Зарплатой считаем только суммы от этого порога — отсекает «401(k)», «50/hr», часы и пр.
    private const long MinPlausibleAmount = 1000;

    public static ParsedSalary Parse(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw))
            return ParsedSalary.Empty;

        var text = raw.ToLowerInvariant();
        var currency = DetectCurrency(text);

        var amounts = AmountRegex().Matches(text)
            .Select(m => ToAmount(m.Groups[1].Value, m.Groups[2].Value))
            .Where(a => a >= MinPlausibleAmount)
            .ToList();

        if (amounts.Count == 0)
            return new ParsedSalary(null, null, currency);

        return new ParsedSalary(amounts.Min(), amounts.Max(), currency);
    }

    private static string? DetectCurrency(string text)
    {
        if (text.Contains('$') || text.Contains("usd")) return "USD";
        if (text.Contains('€') || text.Contains("eur")) return "EUR";
        if (text.Contains('£') || text.Contains("gbp")) return "GBP";
        if (text.Contains('₽') || text.Contains("rub") || text.Contains("руб")) return "RUB";
        return null;
    }

    private static long ToAmount(string number, string suffix)
    {
        if (suffix.Length > 0)
        {
            // «70k» / «70.5k» / «1m»: запятые — разделители тысяч, точка — десятичная.
            var normalized = number.Replace(",", string.Empty);
            if (!double.TryParse(normalized, NumberStyles.Float, CultureInfo.InvariantCulture, out var value))
                return 0;
            // «401k» в тексте — пенсионный план, а не оклад $401 000.
            if (suffix == "k" && value == 401) return 0;
            var multiplier = suffix == "m" ? 1_000_000 : 1_000;
            return (long)(value * multiplier);
        }

        // Без суффикса трактуем и запятую, и точку как разделители тысяч («70,000» / «70.000»).
        var digits = number.Replace(",", string.Empty).Replace(".", string.Empty);
        if (!long.TryParse(digits, NumberStyles.Integer, CultureInfo.InvariantCulture, out var amount))
            return 0;
        // Голое 4-значное число в диапазоне годов — это год («бюджет на 2024»), а не оклад.
        return amount is >= 1900 and <= 2100 ? 0 : amount;
    }

    [GeneratedRegex(@"(\d[\d.,]*)\s*([km])?")]
    private static partial Regex AmountRegex();
}

public sealed record ParsedSalary(long? Min, long? Max, string? Currency)
{
    public static readonly ParsedSalary Empty = new(null, null, null);
}
