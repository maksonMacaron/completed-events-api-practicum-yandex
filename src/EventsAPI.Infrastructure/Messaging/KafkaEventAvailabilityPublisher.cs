using System.Text.Json;
using Confluent.Kafka;
using EventsAPI.Application.Abstractions.Messaging;
using Shared.Contracts;

namespace EventsAPI.Infrastructure.Messaging;

public sealed class KafkaEventAvailabilityPublisher : IEventAvailabilityPublisher, IDisposable
{
    private static readonly JsonSerializerOptions SerializerOptions = new(JsonSerializerDefaults.Web);
    private readonly IProducer<string, string> _producer;

    public KafkaEventAvailabilityPublisher(KafkaOptions options)
    {
        _producer = new ProducerBuilder<string, string>(new ProducerConfig
        {
            BootstrapServers = options.BootstrapServers,
            Acks = Acks.All,
            EnableIdempotence = true
        }).Build();
    }

    public Task PublishAsync(
        EventAvailabilityChanged message,
        CancellationToken cancellationToken = default) =>
        _producer.ProduceAsync(
            KafkaTopics.EventAvailabilityChanged,
            new Message<string, string>
            {
                Key = message.EventId.ToString(),
                Value = JsonSerializer.Serialize(message, SerializerOptions)
            },
            cancellationToken);

    public void Dispose()
    {
        _producer.Flush(TimeSpan.FromSeconds(5));
        _producer.Dispose();
    }
}
