using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using JobRadar.Application.Ingestion;
using JobRadar.Worker.Ingestion;

namespace JobRadar.Worker.Collectors;

/// <summary>
/// Первый C#-коллектор: тянет remote-вакансии из публичного JSON API Remotive и
/// публикует сырьё в Kafka. Устойчивость к 429/таймаутам обеспечивает стандартный
/// resilience-handler на HttpClient (Polly). Скрейпинг-источники остаются на
/// Python как отдельный продюсер в тот же топик.
/// </summary>
public sealed class RemotiveCollector(
    IHttpClientFactory httpClientFactory,
    IVacancyMessageProducer producer,
    ILogger<RemotiveCollector> logger) : BackgroundService
{
    private const string Source = "remotive";
    private static readonly string[] Queries = ["backend", ".net", "golang"];
    private static readonly TimeSpan Interval = TimeSpan.FromMinutes(30);
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    protected override async Task ExecuteAsync(CancellationToken ct)
    {
        using var timer = new PeriodicTimer(Interval);
        do
        {
            try
            {
                await CollectOnceAsync(ct);
            }
            catch (OperationCanceledException) when (ct.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Remotive collection cycle failed");
            }
        }
        while (await timer.WaitForNextTickAsync(ct));
    }

    private async Task CollectOnceAsync(CancellationToken ct)
    {
        var client = httpClientFactory.CreateClient("remotive");
        var produced = 0;

        foreach (var query in Queries)
        {
            var response = await client.GetFromJsonAsync<RemotiveResponse>(
                $"api/remote-jobs?search={Uri.EscapeDataString(query)}", JsonOptions, ct);

            foreach (var job in response?.Jobs ?? [])
            {
                if (!IsRelevant(job.Title))
                    continue;

                await producer.ProduceAsync(new RawVacancyMessage
                {
                    Source = Source,
                    ExternalId = job.Id.ToString(),
                    Title = job.Title,
                    Company = job.CompanyName,
                    Location = job.CandidateRequiredLocation,
                    SalaryRaw = string.IsNullOrWhiteSpace(job.Salary) ? null : job.Salary,
                    Skills = job.Tags is { Count: > 0 } ? string.Join(", ", job.Tags.Take(8)) : null,
                    Url = job.Url,
                    PublishedAt = job.PublicationDate,
                }, ct);
                produced++;
            }
        }

        logger.LogInformation("Remotive: produced {Count} vacancies", produced);
    }

    private static bool IsRelevant(string? title)
    {
        if (string.IsNullOrWhiteSpace(title)) return false;
        var t = title.ToLowerInvariant();
        return Keywords.Any(t.Contains);
    }

    private static readonly string[] Keywords =
        [".net", "c#", "asp.net", "dotnet", "backend", "back-end", "back end",
         "full stack", "full-stack", "fullstack", "golang", "node", "software engineer",
         "software developer", "web developer", "developer"];

    private sealed record RemotiveResponse(
        [property: JsonPropertyName("jobs")] List<RemotiveJob>? Jobs);

    private sealed record RemotiveJob(
        [property: JsonPropertyName("id")] long Id,
        [property: JsonPropertyName("title")] string Title,
        [property: JsonPropertyName("company_name")] string? CompanyName,
        [property: JsonPropertyName("candidate_required_location")] string? CandidateRequiredLocation,
        [property: JsonPropertyName("salary")] string? Salary,
        [property: JsonPropertyName("tags")] List<string>? Tags,
        [property: JsonPropertyName("url")] string? Url,
        [property: JsonPropertyName("publication_date")] DateTimeOffset? PublicationDate);
}
