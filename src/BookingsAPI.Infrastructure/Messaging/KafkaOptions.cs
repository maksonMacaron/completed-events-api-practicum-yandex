namespace BookingsAPI.Infrastructure.Messaging;

public sealed class KafkaOptions
{
    public const string SectionName = "Kafka";

    public string BootstrapServers { get; init; } = string.Empty;
    public string ConsumerGroup { get; init; } = string.Empty;
}
