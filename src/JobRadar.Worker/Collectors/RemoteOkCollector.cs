using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using JobRadar.Application.Ingestion;
using JobRadar.Worker.Ingestion;
using Microsoft.Extensions.Options;

namespace JobRadar.Worker.Collectors;

/// <summary>
/// Второй C#-коллектор: публичный JSON API RemoteOK. Источник перекрывается с
/// Remotive (одни и те же вакансии встречаются на обоих бордах) — на этом
/// проверяется кросс-источниковая дедупликация (DedupKey). Устойчивость к
/// 429/таймаутам — стандартный resilience-handler (Polly) на HttpClient.
/// </summary>
public sealed class RemoteOkCollector(
    IHttpClientFactory httpClientFactory,
    IKafkaPublisher publisher,
    IOptions<KafkaSettings> options,
    ILogger<RemoteOkCollector> logger) : BackgroundService
{
    private const string Source = "remoteok";
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
                logger.LogError(ex, "RemoteOK collection cycle failed");
            }
        }
        while (await timer.WaitForNextTickAsync(ct));
    }

    private async Task CollectOnceAsync(CancellationToken ct)
    {
        var client = httpClientFactory.CreateClient("remoteok");
        var topic = options.Value.RawVacanciesTopic;

        // RemoteOK отдаёт JSON-массив; первый элемент — служебный (legal-нотис) без id.
        var jobs = await client.GetFromJsonAsync<List<RemoteOkJob>>("api", JsonOptions, ct) ?? [];
        var produced = 0;

        foreach (var job in jobs)
        {
            if (string.IsNullOrEmpty(job.Id) || !VacancyRelevance.IsRelevant(job.Position))
                continue;

            await publisher.PublishAsync(topic, $"{Source}:{job.Id}", new RawVacancyMessage
            {
                Source = Source,
                ExternalId = job.Id,
                Title = job.Position!,
                Company = job.Company,
                Location = job.Location,
                SalaryRaw = FormatSalary(job.SalaryMin, job.SalaryMax),
                Skills = job.Tags is { Count: > 0 } ? string.Join(", ", job.Tags.Take(8)) : null,
                Url = job.Url,
                PublishedAt = job.Date,
            }, ct);
            produced++;
        }

        logger.LogInformation("RemoteOK: produced {Count} vacancies", produced);
    }

    private static string? FormatSalary(long? min, long? max)
    {
        if (min is > 0 && max is > 0) return $"{min}-{max} USD";
        if (min is > 0) return $"{min} USD";
        if (max is > 0) return $"{max} USD";
        return null;
    }

    private sealed record RemoteOkJob(
        [property: JsonPropertyName("id")] string? Id,
        [property: JsonPropertyName("position")] string? Position,
        [property: JsonPropertyName("company")] string? Company,
        [property: JsonPropertyName("location")] string? Location,
        [property: JsonPropertyName("tags")] List<string>? Tags,
        [property: JsonPropertyName("salary_min")] long? SalaryMin,
        [property: JsonPropertyName("salary_max")] long? SalaryMax,
        [property: JsonPropertyName("url")] string? Url,
        [property: JsonPropertyName("date")] DateTimeOffset? Date);
}
