using System.Text.Json;
using System.Text.Json.Serialization;
using Confluent.Kafka;
using JobRadar.Application.Ingestion;
using Microsoft.Extensions.Options;

namespace JobRadar.Worker.Ingestion;

/// <summary>
/// Единый продюсер для всех топиков. EnableIdempotence + acks=all: ретраи внутри
/// сессии продюсера не плодят дублей на брокере. Перечислимые поля сериализуются
/// строками (Inserted/Updated), чтобы фронт получал читаемые значения.
/// </summary>
public sealed class KafkaPublisher : IKafkaPublisher, IDisposable
{
    private static readonly JsonSerializerOptions JsonOptions =
        new(JsonSerializerDefaults.Web) { Converters = { new JsonStringEnumConverter() } };

    private readonly IProducer<string, string> _producer;

    public KafkaPublisher(IOptions<KafkaSettings> options)
    {
        _producer = new ProducerBuilder<string, string>(new ProducerConfig
        {
            BootstrapServers = options.Value.BootstrapServers,
            Acks = Acks.All,
            EnableIdempotence = true,
        }).Build();
    }

    public async Task PublishAsync(string topic, string key, object payload, CancellationToken ct)
    {
        var value = JsonSerializer.Serialize(payload, payload.GetType(), JsonOptions);
        await _producer.ProduceAsync(topic, new Message<string, string> { Key = key, Value = value }, ct);
    }

    public void Dispose()
    {
        _producer.Flush(TimeSpan.FromSeconds(10));
        _producer.Dispose();
    }
}
