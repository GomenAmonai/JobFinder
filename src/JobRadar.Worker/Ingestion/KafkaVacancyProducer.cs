using System.Text.Json;
using Confluent.Kafka;
using JobRadar.Application.Ingestion;
using Microsoft.Extensions.Options;

namespace JobRadar.Worker.Ingestion;

/// <summary>
/// Продюсер сырых вакансий в Kafka. Включены <c>EnableIdempotence</c> + acks=all:
/// повторная отправка при ретраях внутри сессии продюсера не плодит дублей на
/// уровне брокера. Ключ = "{Source}:{ExternalId}" — стабильное партиционирование.
/// </summary>
public sealed class KafkaVacancyProducer : IVacancyMessageProducer, IDisposable
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    private readonly IProducer<string, string> _producer;
    private readonly string _topic;

    public KafkaVacancyProducer(IOptions<KafkaSettings> options)
    {
        var settings = options.Value;
        _topic = settings.RawVacanciesTopic;
        _producer = new ProducerBuilder<string, string>(new ProducerConfig
        {
            BootstrapServers = settings.BootstrapServers,
            Acks = Acks.All,
            EnableIdempotence = true,
        }).Build();
    }

    public async Task ProduceAsync(RawVacancyMessage message, CancellationToken ct)
    {
        var key = $"{message.Source}:{message.ExternalId}";
        var value = JsonSerializer.Serialize(message, JsonOptions);
        await _producer.ProduceAsync(_topic, new Message<string, string> { Key = key, Value = value }, ct);
    }

    public void Dispose()
    {
        _producer.Flush(TimeSpan.FromSeconds(10));
        _producer.Dispose();
    }
}
