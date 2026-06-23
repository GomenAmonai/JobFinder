namespace JobRadar.Worker.Ingestion;

/// <summary>Публикация JSON-сообщения в Kafka с заданным ключом партиционирования.</summary>
public interface IKafkaPublisher
{
    Task PublishAsync(string topic, string key, object payload, CancellationToken ct);
}
