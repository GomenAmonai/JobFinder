using System.Text.Json;
using Confluent.Kafka;
using JobRadar.Application.Ingestion;
using Microsoft.Extensions.Options;

namespace JobRadar.Worker.Ingestion;

/// <summary>
/// Потребитель топика <c>vacancies.raw</c>: нормализует сообщение и идемпотентно
/// сохраняет вакансию. Оффсет коммитится ТОЛЬКО после успешного upsert
/// (EnableAutoCommit=false): at-least-once доставка + идемпотентный приём =
/// эффективно exactly-once на стороне БД.
/// </summary>
public sealed class VacancyConsumer(
    IServiceScopeFactory scopeFactory,
    IOptions<KafkaSettings> options,
    ILogger<VacancyConsumer> logger) : BackgroundService
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

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

                if (result?.Message is null)
                    continue;

                try
                {
                    var message = JsonSerializer.Deserialize<RawVacancyMessage>(result.Message.Value, JsonOptions);
                    if (message is not null)
                    {
                        await using var scope = scopeFactory.CreateAsyncScope();
                        var upsert = scope.ServiceProvider.GetRequiredService<IVacancyUpsertService>();
                        var outcome = await upsert.UpsertAsync(VacancyMapper.ToVacancy(message), ct);
                        logger.LogDebug("{Outcome} {Source}:{Id}", outcome.Outcome, message.Source, message.ExternalId);
                    }

                    consumer.Commit(result);
                }
                catch (JsonException ex)
                {
                    // Битое сообщение не должно навсегда блокировать партишн — логируем и пропускаем.
                    logger.LogError(ex, "Skipping malformed message at {Offset}", result.TopicPartitionOffset);
                    consumer.Commit(result);
                }
            }
        }
        finally
        {
            consumer.Close();
        }
    }
}
