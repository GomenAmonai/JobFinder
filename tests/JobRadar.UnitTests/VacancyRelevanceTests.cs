using FluentAssertions;
using JobRadar.Application.Ingestion;
using Xunit;

namespace JobRadar.UnitTests;

public class VacancyRelevanceTests
{
    [Fact]
    public void Dotnet_title_is_relevant()
        => VacancyRelevance.IsRelevant(".NET Developer").Should().BeTrue();

    [Fact]
    public void Unrelated_title_is_not_relevant()
        => VacancyRelevance.IsRelevant("Marketing Manager").Should().BeFalse();
}
