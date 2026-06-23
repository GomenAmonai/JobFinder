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
                    // Битое сообщение не должно навсегда блокировать партишн — логируем и пропускаем.
                    logger.LogError(ex, "Skipping malformed message at {Offset}", result.TopicPartitionOffset);
                    consumer.Commit(result);
                }
                catch (OperationCanceledException)
                {
                    break;
                }
                catch (Exception ex)
                {
                    // Сбой обработки одной записи не должен ронять весь конвейер;
                    // в проде такие сообщения уходили бы в dead-letter topic.
                    logger.LogError(ex, "Processing failed, skipping message at {Offset}", result.TopicPartitionOffset);
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
