using FluentAssertions;
using JobRadar.Application.Ingestion;
using Xunit;

namespace JobRadar.UnitTests;

public class VacancyNormalizationTests
{
    [Fact]
    public void NormalizeMarket_maps_tokyo_location_to_japan()
        => VacancyNormalization.NormalizeMarket("Tokyo, Japan").Should().Be("Япония");

    [Fact]
    public void NormalizeMarket_maps_unknown_location_to_other()
        => VacancyNormalization.NormalizeMarket("Atlantis").Should().Be("Другое");

    [Fact]
    public void GuessLevel_detects_junior_from_title()
        => VacancyNormalization.GuessLevel("Junior Backend Developer").Should().Be("junior");

    [Fact]
    public void GuessLevel_detects_senior_from_title()
        => VacancyNormalization.GuessLevel("Lead Software Engineer").Should().Be("senior+");

    [Fact]
    public void DetectStack_flags_dotnet_titles()
        => VacancyNormalization.DetectStack("Senior C# / ASP.NET Engineer").Should().Be("C#/.NET");
}
