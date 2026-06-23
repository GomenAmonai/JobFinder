using System.Text.Json;
using System.Text.Json.Serialization;
using Confluent.Kafka;
using JobRadar.Api.Hubs;
using JobRadar.Application.Ingestion;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Options;

namespace JobRadar.Api.Ingestion;

/// <summary>
/// Мост Kafka → SignalR: потребляет <c>vacancies.changed</c> и рассылает событие
/// <c>VacancyChanged</c> всем подключённым клиентам. Так воркер (продюсер) и API
/// (раздача) остаются развязанными процессами. Читает только новые сообщения
/// (offset=Latest) — история клиенту при подключении не нужна.
/// </summary>
public sealed class VacancyChangedConsumer(
    IHubContext<VacancyHub> hub,
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
                        await hub.Clients.All.SendAsync("VacancyChanged", changed, ct);
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
