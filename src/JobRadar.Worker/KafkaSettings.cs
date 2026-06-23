namespace JobRadar.Worker;

public sealed class KafkaSettings
{
    public const string SectionName = "Kafka";

    public string BootstrapServers { get; set; } = "localhost:9092";
    public string RawVacanciesTopic { get; set; } = "vacancies.raw";
    public string ConsumerGroup { get; set; } = "jobradar-normalizer";
}
