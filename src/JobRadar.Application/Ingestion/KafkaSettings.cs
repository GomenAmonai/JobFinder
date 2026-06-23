namespace JobRadar.Application.Ingestion;

/// <summary>
/// Конфигурация Kafka, общая для воркера (производит/потребляет raw) и API
/// (потребляет changed для SignalR). ConsumerGroup задаётся в appsettings каждого
/// хоста отдельно — у нормализатора и у SignalR-раздачи разные группы.
/// </summary>
public sealed class KafkaSettings
{
    public const string SectionName = "Kafka";

    public string BootstrapServers { get; set; } = "localhost:9092";
    public string RawVacanciesTopic { get; set; } = "vacancies.raw";
    public string ChangedVacanciesTopic { get; set; } = "vacancies.changed";
    public string ConsumerGroup { get; set; } = "jobradar";
}
