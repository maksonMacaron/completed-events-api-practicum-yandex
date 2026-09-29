using Microsoft.Extensions.Configuration;

namespace Shared.Contracts.Infrastructure;

public sealed class KafkaOptions
{
    public const string SectionName = "Kafka";

    public string BootstrapServers { get; init; } = string.Empty;
    public string ConsumerGroup { get; init; } = string.Empty;

    public static KafkaOptions FromConfiguration(IConfiguration configuration)
    {
        var section = configuration.GetSection(SectionName);
        var options = new KafkaOptions
        {
            BootstrapServers = section[nameof(BootstrapServers)] ?? string.Empty,
            ConsumerGroup = section[nameof(ConsumerGroup)] ?? string.Empty
        };

        if (string.IsNullOrWhiteSpace(options.BootstrapServers)
            || string.IsNullOrWhiteSpace(options.ConsumerGroup))
        {
            throw new InvalidOperationException("Параметры Kafka заполнены некорректно");
        }

        return options;
    }
}
