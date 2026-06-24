using FluentAssertions;
using JobRadar.Application.Ingestion;
using Xunit;

namespace JobRadar.UnitTests;

public class DedupKeyBuilderTests
{
    [Fact]
    public void Build_is_stable_across_case_and_punctuation()
        => DedupKeyBuilder.Build("Acme, Inc.", "Senior .NET Developer")
            .Should().Be(DedupKeyBuilder.Build("ACME   INC", "senior  .net   developer"));

    [Fact]
    public void Build_returns_null_without_a_company()
        => DedupKeyBuilder.Build(null, "Developer").Should().BeNull();
}
