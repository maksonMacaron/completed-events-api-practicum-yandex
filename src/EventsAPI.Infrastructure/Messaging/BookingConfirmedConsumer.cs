using System.Text.Json;
using Confluent.Kafka;
using EventsAPI.Application.Abstractions.Persistence;
using EventsAPI.Application.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Shared.Contracts;

namespace EventsAPI.Infrastructure.Messaging;

public sealed class BookingConfirmedConsumer : BackgroundService
{
    private static readonly JsonSerializerOptions SerializerOptions = new(JsonSerializerDefaults.Web);

    private readonly KafkaOptions _options;
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<BookingConfirmedConsumer> _logger;

    public BookingConfirmedConsumer(
        KafkaOptions options,
        IServiceScopeFactory scopeFactory,
        ILogger<BookingConfirmedConsumer> logger)
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

        consumer.Subscribe(KafkaTopics.BookingConfirmed);

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
                    _logger.LogWarning(exception, "Ошибка чтения сообщения из Kafka");
                    continue;
                }

                BookingConfirmed? message;
                try
                {
                    message = JsonSerializer.Deserialize<BookingConfirmed>(
                        result.Message.Value,
                        SerializerOptions);
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
                    _logger.LogWarning(
                        "Сообщение {Offset} не содержит данных",
                        result.TopicPartitionOffset);
                    consumer.Commit(result);
                    continue;
                }

                try
                {
                    await using var scope = _scopeFactory.CreateAsyncScope();
                    var handler = scope.ServiceProvider.GetRequiredService<IBookingConfirmedHandler>();
                    var processingResult = await handler.HandleAsync(message, stoppingToken);
                    LogProcessingResult(message, processingResult);

                    if (processingResult is BookingConfirmationResult.EventNotFound
                        or BookingConfirmationResult.NotEnoughSeats)
                    {
                        consumer.Seek(result.TopicPartitionOffset);
                        await Task.Delay(TimeSpan.FromSeconds(2), stoppingToken);
                        continue;
                    }

                    consumer.Commit(result);
                }
                catch (Exception exception) when (!stoppingToken.IsCancellationRequested)
                {
                    _logger.LogError(
                        exception,
                        "Не удалось обработать бронь {BookingId}",
                        message.BookingId);
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

    private void LogProcessingResult(
        BookingConfirmed message,
        BookingConfirmationResult result)
    {
        switch (result)
        {
            case BookingConfirmationResult.Applied:
                _logger.LogInformation(
                    "Для события {EventId} списано мест: {Seats}",
                    message.EventId,
                    message.Seats);
                break;
            case BookingConfirmationResult.AlreadyProcessed:
                _logger.LogInformation(
                    "Бронь {BookingId} уже была обработана",
                    message.BookingId);
                break;
            case BookingConfirmationResult.EventNotFound:
                _logger.LogWarning(
                    "Событие {EventId} для брони {BookingId} не найдено",
                    message.EventId,
                    message.BookingId);
                break;
            case BookingConfirmationResult.NotEnoughSeats:
                _logger.LogWarning(
                    "Для события {EventId} недостаточно свободных мест",
                    message.EventId);
                break;
        }
    }
}
