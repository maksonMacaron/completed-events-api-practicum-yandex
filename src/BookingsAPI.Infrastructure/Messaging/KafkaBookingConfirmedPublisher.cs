using System.Text.Json;
using BookingsAPI.Application.Abstractions.Messaging;
using Confluent.Kafka;
using Shared.Contracts;
using Shared.Contracts.Infrastructure;

namespace BookingsAPI.Infrastructure.Messaging;

public sealed class KafkaBookingConfirmedPublisher : IBookingConfirmedPublisher, IDisposable
{
    private readonly IProducer<string, string> _producer;

    public KafkaBookingConfirmedPublisher(KafkaOptions options)
    {
        _producer = new ProducerBuilder<string, string>(new ProducerConfig
        {
            BootstrapServers = options.BootstrapServers,
            Acks = Acks.All,
            EnableIdempotence = true
        }).Build();
    }

    public Task PublishAsync(
        BookingConfirmed message,
        CancellationToken cancellationToken = default) =>
        _producer.ProduceAsync(
            KafkaTopics.BookingConfirmed,
            new Message<string, string>
            {
                Key = message.EventId.ToString(),
                Value = JsonSerializer.Serialize(message, KafkaJsonSerializer.Options)
            },
            cancellationToken);

    public void Dispose()
    {
        _producer.Flush(TimeSpan.FromSeconds(5));
        _producer.Dispose();
    }
}
