using Confluent.Kafka;
using Confluent.Kafka.Admin;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Shared.Contracts;

namespace EventsAPI.Infrastructure.Messaging;

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
            await adminClient.CreateTopicsAsync(
                [new TopicSpecification
                {
                    Name = KafkaTopics.BookingConfirmed,
                    NumPartitions = 3,
                    ReplicationFactor = 1
                }]);

            _logger.LogInformation(
                "Топик {TopicName} создан",
                KafkaTopics.BookingConfirmed);
        }
        catch (CreateTopicsException exception)
            when (exception.Results.All(result => result.Error.Code == ErrorCode.TopicAlreadyExists))
        {
            _logger.LogInformation(
                "Топик {TopicName} уже существует",
                KafkaTopics.BookingConfirmed);
        }
        catch (Exception exception)
        {
            _logger.LogWarning(
                exception,
                "Не удалось проверить или создать топик {TopicName}",
                KafkaTopics.BookingConfirmed);
        }
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}
