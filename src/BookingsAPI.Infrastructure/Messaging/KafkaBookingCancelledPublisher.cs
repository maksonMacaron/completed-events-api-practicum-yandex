using System.Text.Json;
using BookingsAPI.Application.Abstractions.Messaging;
using Confluent.Kafka;
using Shared.Contracts;
using Shared.Contracts.Infrastructure;

namespace BookingsAPI.Infrastructure.Messaging;

public sealed class KafkaBookingCancelledPublisher : IBookingCancelledPublisher, IDisposable
{
    private readonly IProducer<string, string> _producer;

    public KafkaBookingCancelledPublisher(KafkaOptions options)
    {
        _producer = new ProducerBuilder<string, string>(new ProducerConfig
        {
            BootstrapServers = options.BootstrapServers,
            Acks = Acks.All,
            EnableIdempotence = true
        }).Build();
    }

    public Task PublishAsync(
        BookingCancelled message,
        CancellationToken cancellationToken = default) =>
        _producer.ProduceAsync(
            KafkaTopics.BookingCancelled,
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
