using FluentAssertions;
using JobRadar.Application.Ingestion;
using Xunit;

namespace JobRadar.UnitTests;

public class VacancyMapperTests
{
    [Fact]
    public void ToVacancy_normalizes_published_date_to_utc()
    {
        var message = new RawVacancyMessage
        {
            Source = "remotive",
            ExternalId = "1",
            Title = "Backend Developer",
            PublishedAt = new DateTimeOffset(2026, 6, 16, 10, 0, 0, TimeSpan.FromHours(3)),
        };

        var vacancy = VacancyMapper.ToVacancy(message);

        vacancy.PublishedAt!.Value.Offset.Should().Be(TimeSpan.Zero);
    }
}
