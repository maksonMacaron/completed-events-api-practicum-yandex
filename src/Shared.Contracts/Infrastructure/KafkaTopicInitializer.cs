using Confluent.Kafka;
using Confluent.Kafka.Admin;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Shared.Contracts.Infrastructure;

public sealed class KafkaTopicInitializer : IHostedService
{
    private readonly KafkaOptions _options;
    private readonly ILogger<KafkaTopicInitializer> _logger;

    public KafkaTopicInitializer(
        KafkaOptions options,
        ILogger<KafkaTopicInitializer> logger)
    {
        _options = options;
        _logger = logger;
    }

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        using var adminClient = new AdminClientBuilder(new AdminClientConfig
        {
            BootstrapServers = _options.BootstrapServers
        }).Build();

        try
        {
            await adminClient.CreateTopicsAsync(CreateTopicSpecifications());
            _logger.LogInformation("Топики Kafka созданы");
        }
        catch (CreateTopicsException exception)
            when (exception.Results.All(result =>
                result.Error.Code is ErrorCode.NoError or ErrorCode.TopicAlreadyExists))
        {
            _logger.LogInformation("Топики Kafka уже существуют");
        }
        catch (Exception exception)
        {
            _logger.LogWarning(exception, "Не удалось проверить или создать топики Kafka");
        }
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;

    private static TopicSpecification[] CreateTopicSpecifications() =>
    [
        CreateTopic(KafkaTopics.BookingConfirmed),
        CreateTopic(KafkaTopics.BookingCancelled),
        CreateTopic(KafkaTopics.EventAvailabilityChanged)
    ];

    private static TopicSpecification CreateTopic(string name) => new()
    {
        Name = name,
        NumPartitions = 3,
        ReplicationFactor = 1
    };
}
