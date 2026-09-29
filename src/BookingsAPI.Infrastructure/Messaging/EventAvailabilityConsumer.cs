using System.Text.Json;
using BookingsAPI.Application.Abstractions.Persistence;
using Confluent.Kafka;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Shared.Contracts;
using Shared.Contracts.Infrastructure;

namespace BookingsAPI.Infrastructure.Messaging;

public sealed class EventAvailabilityConsumer : BackgroundService
{
    private readonly KafkaOptions _options;
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<EventAvailabilityConsumer> _logger;

    public EventAvailabilityConsumer(
        KafkaOptions options,
        IServiceScopeFactory scopeFactory,
        ILogger<EventAvailabilityConsumer> logger)
    {
        _options = options;
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await Task.Yield();

        using var consumer = new ConsumerBuilder<string, string>(new ConsumerConfig
        {
            BootstrapServers = _options.BootstrapServers,
            GroupId = _options.ConsumerGroup,
            AutoOffsetReset = AutoOffsetReset.Earliest,
            EnableAutoCommit = false,
            AllowAutoCreateTopics = false
        }).Build();

        consumer.Subscribe(KafkaTopics.EventAvailabilityChanged);

        try
        {
            while (!stoppingToken.IsCancellationRequested)
            {
                ConsumeResult<string, string> result;
                try
                {
                    result = await Task.Run(() => consumer.Consume(stoppingToken), stoppingToken);
                }
                catch (ConsumeException exception)
                {
                    _logger.LogWarning(exception, "Ошибка чтения каталога событий из Kafka");
                    continue;
                }

                EventAvailabilityChanged? message;
                try
                {
                    message = JsonSerializer.Deserialize<EventAvailabilityChanged>(
                        result.Message.Value,
                        KafkaJsonSerializer.Options);
                }
                catch (JsonException exception)
                {
                    _logger.LogError(
                        exception,
                        "Сообщение {Offset} содержит некорректный JSON",
                        result.TopicPartitionOffset);
                    consumer.Commit(result);
                    continue;
                }

                if (message is null)
                {
                    consumer.Commit(result);
                    continue;
                }

                try
                {
                    await using var scope = _scopeFactory.CreateAsyncScope();
                    var eventCatalog = scope.ServiceProvider.GetRequiredService<IEventCatalog>();
                    await eventCatalog.ApplyAsync(
                        message.EventId,
                        message.IsAvailable,
                        message.ChangedAt,
                        stoppingToken);
                    consumer.Commit(result);
                }
                catch (Exception exception) when (!stoppingToken.IsCancellationRequested)
                {
                    _logger.LogError(
                        exception,
                        "Не удалось обновить доступность события {EventId}",
                        message.EventId);
                    consumer.Seek(result.TopicPartitionOffset);
                    await Task.Delay(TimeSpan.FromSeconds(2), stoppingToken);
                }
            }
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
        }
        finally
        {
            consumer.Close();
        }
    }
}
