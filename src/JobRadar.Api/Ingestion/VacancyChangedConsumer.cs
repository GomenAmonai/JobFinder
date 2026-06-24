using System.Text.Json;
using System.Text.Json.Serialization;
using Confluent.Kafka;
using JobRadar.Api.Hubs;
using JobRadar.Application.Ingestion;
using JobRadar.Application.SavedFilters;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Options;

namespace JobRadar.Api.Ingestion;

/// <summary>
/// Мост Kafka → SignalR: потребляет <c>vacancies.changed</c>. Всем клиентам шлёт
/// <c>VacancyChanged</c> (публичная live-лента); дополнительно — таргетированный
/// <c>MatchedVacancy</c> тем аутентифицированным юзерам, чей сохранённый фильтр
/// совпал. Воркер (продюсер) и API (раздача) остаются развязанными процессами.
/// Читает только новые сообщения (offset=Latest).
/// </summary>
public sealed class VacancyChangedConsumer(
    IHubContext<VacancyHub> hub,
    IServiceScopeFactory scopeFactory,
    IOptions<KafkaSettings> options,
    ILogger<VacancyChangedConsumer> logger) : BackgroundService
{
    private static readonly JsonSerializerOptions JsonOptions =
        new(JsonSerializerDefaults.Web) { Converters = { new JsonStringEnumConverter() } };

    protected override Task ExecuteAsync(CancellationToken stoppingToken)
        => Task.Run(() => ConsumeLoop(stoppingToken), stoppingToken);

    private async Task ConsumeLoop(CancellationToken ct)
    {
        var settings = options.Value;
        using var consumer = new ConsumerBuilder<string, string>(new ConsumerConfig
        {
            BootstrapServers = settings.BootstrapServers,
            GroupId = settings.ConsumerGroup,
            AutoOffsetReset = AutoOffsetReset.Latest,
            EnableAutoCommit = true,
        }).Build();

        consumer.Subscribe(settings.ChangedVacanciesTopic);
        logger.LogInformation("Broadcasting {Topic} over SignalR", settings.ChangedVacanciesTopic);

        try
        {
            while (!ct.IsCancellationRequested)
            {
                ConsumeResult<string, string> result;
                try
                {
                    result = consumer.Consume(ct);
                }
                catch (OperationCanceledException)
                {
                    break;
                }
                catch (ConsumeException ex)
                {
                    logger.LogWarning("Consume error: {Reason}", ex.Error.Reason);
                    if (!await DelayAsync(ct)) break;
                    continue;
                }

                if (result?.Message?.Value is null)
                    continue;

                try
                {
                    var changed = JsonSerializer.Deserialize<VacancyChangedEvent>(result.Message.Value, JsonOptions);
                    if (changed is not null)
                    {
                        await hub.Clients.All.SendAsync("VacancyChanged", changed, ct);
                        await PushMatchesAsync(changed, ct);
                    }
                }
                catch (JsonException ex)
                {
                    logger.LogError(ex, "Skipping malformed change event at {Offset}", result.TopicPartitionOffset);
                }
            }
        }
        finally
        {
            consumer.Close();
        }
    }

    private async Task PushMatchesAsync(VacancyChangedEvent changed, CancellationToken ct)
    {
        await using var scope = scopeFactory.CreateAsyncScope();
        var matcher = scope.ServiceProvider.GetRequiredService<ISavedFilterMatcher>();
        var userIds = await matcher.FindMatchingUserIdsAsync(changed.Market, changed.Level, changed.Stack, changed.Title, ct);
        foreach (var userId in userIds)
            await hub.Clients.User(userId.ToString()).SendAsync("MatchedVacancy", changed, ct);
    }

    private static async Task<bool> DelayAsync(CancellationToken ct)
    {
        try
        {
            await Task.Delay(TimeSpan.FromSeconds(1), ct);
            return true;
        }
        catch (OperationCanceledException)
        {
            return false;
        }
    }
}
