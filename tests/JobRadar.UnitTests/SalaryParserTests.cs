using FluentAssertions;
using JobRadar.Application.Ingestion;
using Xunit;

namespace JobRadar.UnitTests;

public class SalaryParserTests
{
    [Fact]
    public void Parse_reads_a_dollar_range()
        => SalaryParser.Parse("$70,000 - $90,000").Should().Be(new ParsedSalary(70000, 90000, "USD"));

    [Fact]
    public void Parse_expands_the_k_suffix()
        => SalaryParser.Parse("$70k - $90k").Should().Be(new ParsedSalary(70000, 90000, "USD"));

    [Fact]
    public void Parse_reads_a_single_euro_amount()
        => SalaryParser.Parse("€50000").Should().Be(new ParsedSalary(50000, 50000, "EUR"));

    [Fact]
    public void Parse_returns_empty_for_unparseable_text()
        => SalaryParser.Parse("Competitive salary").Should().Be(ParsedSalary.Empty);

    [Fact]
    public void Parse_keeps_currency_when_amount_is_absent()
        => SalaryParser.Parse("USD, negotiable").Should().Be(new ParsedSalary(null, null, "USD"));

    [Fact]
    public void Parse_ignores_401k_retirement_mentions()
        => SalaryParser.Parse("$60,000 + 401k match").Should().Be(new ParsedSalary(60000, 60000, "USD"));

    [Fact]
    public void Parse_ignores_year_numbers()
        => SalaryParser.Parse("2024 budget: 80000 USD").Should().Be(new ParsedSalary(80000, 80000, "USD"));
}
