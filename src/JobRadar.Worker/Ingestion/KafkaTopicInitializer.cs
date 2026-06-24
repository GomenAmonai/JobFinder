using Confluent.Kafka;
using Confluent.Kafka.Admin;
using JobRadar.Application.Ingestion;
using Microsoft.Extensions.Options;

namespace JobRadar.Worker.Ingestion;

/// <summary>
/// Создаёт топики до старта консьюмера/коллектора, чтобы избежать гонки
/// «Unknown topic or partition» и сделать набор топиков явным, а не полагаться на
/// broker auto-create. Регистрируется первым hosted-сервисом; идемпотентен.
/// </summary>
public sealed class KafkaTopicInitializer(IOptions<KafkaSettings> options, ILogger<KafkaTopicInitializer> logger)
    : IHostedService
{
    public async Task StartAsync(CancellationToken cancellationToken)
    {
        var settings = options.Value;
        using var admin = new AdminClientBuilder(new AdminClientConfig
        {
            BootstrapServers = settings.BootstrapServers,
        }).Build();

        var topics = new[] { settings.RawVacanciesTopic, settings.ChangedVacanciesTopic, settings.DeadLetterTopic }
            .Select(name => new TopicSpecification { Name = name, NumPartitions = 1, ReplicationFactor = 1 })
            .ToList();

        try
        {
            await admin.CreateTopicsAsync(topics);
            logger.LogInformation("Kafka topics ensured: {Topics}", string.Join(", ", topics.Select(t => t.Name)));
        }
        catch (CreateTopicsException ex) when (ex.Results.All(r => r.Error.Code is ErrorCode.NoError or ErrorCode.TopicAlreadyExists))
        {
            // топики уже существуют — это норма
        }
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}
