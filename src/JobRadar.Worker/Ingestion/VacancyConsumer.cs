using System.Text.Json;
using System.Text.Json.Serialization;
using Confluent.Kafka;
using JobRadar.Application.Ingestion;
using Microsoft.Extensions.Options;

namespace JobRadar.Worker.Ingestion;

/// <summary>
/// Потребитель топика <c>vacancies.raw</c>: нормализует сообщение, идемпотентно
/// сохраняет вакансию и публикует <see cref="VacancyChangedEvent"/> в
/// <c>vacancies.changed</c> (для SignalR на стороне API). Оффсет коммитится только
/// после успешной обработки (EnableAutoCommit=false): at-least-once доставка +
/// идемпотентный upsert = эффективно exactly-once на стороне БД.
/// </summary>
public sealed class VacancyConsumer(
    IServiceScopeFactory scopeFactory,
    IKafkaPublisher publisher,
    IOptions<KafkaSettings> options,
    ILogger<VacancyConsumer> logger) : BackgroundService
{
    private static readonly JsonSerializerOptions JsonOptions =
        new(JsonSerializerDefaults.Web) { Converters = { new JsonStringEnumConverter() } };

    // Consume() у Confluent.Kafka синхронный и блокирующий — уводим цикл с потока старта хоста.
    protected override Task ExecuteAsync(CancellationToken stoppingToken)
        => Task.Run(() => ConsumeLoop(stoppingToken), stoppingToken);

    private async Task ConsumeLoop(CancellationToken ct)
    {
        var settings = options.Value;
        using var consumer = new ConsumerBuilder<string, string>(new ConsumerConfig
        {
            BootstrapServers = settings.BootstrapServers,
            GroupId = settings.ConsumerGroup,
            AutoOffsetReset = AutoOffsetReset.Earliest,
            EnableAutoCommit = false,
        }).Build();

        consumer.Subscribe(settings.RawVacanciesTopic);
        logger.LogInformation("Consuming {Topic} as group {Group}", settings.RawVacanciesTopic, settings.ConsumerGroup);

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
                    // Транзиентные ошибки брокера (напр. топик ещё прогревается) не должны
                    // ронять хост — логируем, ждём и продолжаем.
                    logger.LogWarning("Consume error: {Reason}", ex.Error.Reason);
                    if (!await DelayAsync(ct)) break;
                    continue;
                }

                if (result?.Message is null)
                    continue;

                try
                {
                    var message = JsonSerializer.Deserialize<RawVacancyMessage>(result.Message.Value, JsonOptions);
                    if (message is not null)
                        await ProcessAsync(message, ct);

                    consumer.Commit(result);
                }
                catch (JsonException ex)
                {
                    // Битое сообщение не должно навсегда блокировать партишн — в dead-letter и дальше.
                    logger.LogError(ex, "Malformed message at {Offset} -> dead-letter", result.TopicPartitionOffset);
                    await PublishDeadLetterAsync(result, ex, ct);
                    consumer.Commit(result);
                }
                catch (OperationCanceledException)
                {
                    break;
                }
                catch (Exception ex)
                {
                    // Сбой обработки одной записи не должен ронять конвейер — сохраняем в dead-letter.
                    logger.LogError(ex, "Processing failed at {Offset} -> dead-letter", result.TopicPartitionOffset);
                    await PublishDeadLetterAsync(result, ex, ct);
                    consumer.Commit(result);
                }
            }
        }
        finally
        {
            consumer.Close();
        }
    }

    private async Task ProcessAsync(RawVacancyMessage message, CancellationToken ct)
    {
        var vacancy = VacancyMapper.ToVacancy(message);

        await using var scope = scopeFactory.CreateAsyncScope();
        var upsert = scope.ServiceProvider.GetRequiredService<IVacancyUpsertService>();
        var outcome = await upsert.UpsertAsync(vacancy, ct);
        logger.LogDebug("{Outcome} {Source}:{Id}", outcome, message.Source, message.ExternalId);

        await publisher.PublishAsync(
            options.Value.ChangedVacanciesTopic,
            $"{vacancy.Source}:{vacancy.ExternalId}",
            new VacancyChangedEvent
            {
                Source = vacancy.Source,
                ExternalId = vacancy.ExternalId,
                Title = vacancy.Title,
                Company = vacancy.Company,
                Market = vacancy.Market,
                Level = vacancy.Level,
                Stack = vacancy.Stack,
                Url = vacancy.Url,
                PublishedAt = vacancy.PublishedAt,
                Outcome = outcome,
            },
            ct);
    }

    private async Task PublishDeadLetterAsync(ConsumeResult<string, string> result, Exception ex, CancellationToken ct)
    {
        try
        {
            await publisher.PublishAsync(options.Value.DeadLetterTopic, result.Message.Key, new DeadLetterEnvelope
            {
                SourceTopic = options.Value.RawVacanciesTopic,
                Key = result.Message.Key,
                Payload = result.Message.Value,
                Error = ex.Message,
                Offset = result.TopicPartitionOffset.ToString(),
                FailedAt = DateTimeOffset.UtcNow,
            }, ct);
        }
        catch (Exception dlqEx)
        {
            // Падение публикации в dead-letter не должно ронять консьюмер — лог и дальше.
            logger.LogError(dlqEx, "Failed to publish to dead-letter topic");
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
