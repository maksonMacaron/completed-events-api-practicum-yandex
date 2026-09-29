using System.Text.Json;
using BookingsAPI.Application.Abstractions.Messaging;
using BookingsAPI.Application.Abstractions.Persistence;
using BookingsAPI.Domain.Entities;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Shared.Contracts;
using Shared.Contracts.Infrastructure;

namespace BookingsAPI.Infrastructure.BackgroundServices;

public sealed class OutboxPublisherWorker : BackgroundService
{
    private static readonly TimeSpan MinimumPollingInterval = TimeSpan.FromSeconds(1);
    private static readonly TimeSpan MaximumPollingInterval = TimeSpan.FromSeconds(30);

    private readonly IServiceScopeFactory _scopeFactory;
    private readonly IBookingConfirmedPublisher _confirmedPublisher;
    private readonly IBookingCancelledPublisher _cancelledPublisher;
    private readonly ILogger<OutboxPublisherWorker> _logger;

    public OutboxPublisherWorker(
        IServiceScopeFactory scopeFactory,
        IBookingConfirmedPublisher confirmedPublisher,
        IBookingCancelledPublisher cancelledPublisher,
        ILogger<OutboxPublisherWorker> logger)
    {
        _scopeFactory = scopeFactory;
        _confirmedPublisher = confirmedPublisher;
        _cancelledPublisher = cancelledPublisher;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var pollingInterval = MinimumPollingInterval;

        while (!stoppingToken.IsCancellationRequested)
        {
            IReadOnlyList<OutboxMessage> messages = [];
            try
            {
                await using (var scope = _scopeFactory.CreateAsyncScope())
                {
                    var repository = scope.ServiceProvider.GetRequiredService<IOutboxRepository>();
                    messages = await repository.GetPendingAsync(stoppingToken);
                }

                foreach (var message in messages)
                {
                    try
                    {
                        await PublishAsync(message, stoppingToken);

                        await using var scope = _scopeFactory.CreateAsyncScope();
                        var repository = scope.ServiceProvider.GetRequiredService<IOutboxRepository>();
                        await repository.MarkPublishedAsync(message.Id, stoppingToken);
                    }
                    catch (Exception exception) when (!stoppingToken.IsCancellationRequested)
                    {
                        _logger.LogWarning(
                            exception,
                            "Не удалось опубликовать outbox-сообщение {MessageId}",
                            message.Id);
                    }
                }
            }
            catch (Exception exception) when (!stoppingToken.IsCancellationRequested)
            {
                _logger.LogError(exception, "Ошибка обработки outbox");
            }

            pollingInterval = messages.Count == 0
                ? IncreaseDelay(pollingInterval)
                : MinimumPollingInterval;
            await Task.Delay(pollingInterval, stoppingToken);
        }
    }

    private Task PublishAsync(
        OutboxMessage message,
        CancellationToken cancellationToken) =>
        message.Type switch
        {
            nameof(BookingConfirmed) => _confirmedPublisher.PublishAsync(
                Deserialize<BookingConfirmed>(message),
                cancellationToken),
            nameof(BookingCancelled) => _cancelledPublisher.PublishAsync(
                Deserialize<BookingCancelled>(message),
                cancellationToken),
            _ => throw new InvalidOperationException(
                $"Неизвестный тип outbox-сообщения: {message.Type}")
        };

    private static T Deserialize<T>(OutboxMessage message) =>
        JsonSerializer.Deserialize<T>(message.Payload, KafkaJsonSerializer.Options)
        ?? throw new InvalidOperationException(
            $"Outbox-сообщение {message.Id} не содержит данных");

    private static TimeSpan IncreaseDelay(TimeSpan current) =>
        TimeSpan.FromSeconds(Math.Min(current.TotalSeconds * 2, MaximumPollingInterval.TotalSeconds));
}
