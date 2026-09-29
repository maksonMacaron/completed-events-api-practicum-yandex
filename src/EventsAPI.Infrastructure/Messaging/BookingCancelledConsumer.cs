using System.Text.Json;
using Confluent.Kafka;
using EventsAPI.Application.Abstractions.Persistence;
using EventsAPI.Application.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Shared.Contracts;

namespace EventsAPI.Infrastructure.Messaging;

public sealed class BookingCancelledConsumer : BackgroundService
{
    private static readonly JsonSerializerOptions SerializerOptions = new(JsonSerializerDefaults.Web);

    private readonly KafkaOptions _options;
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<BookingCancelledConsumer> _logger;

    public BookingCancelledConsumer(
        KafkaOptions options,
        IServiceScopeFactory scopeFactory,
        ILogger<BookingCancelledConsumer> logger)
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
            GroupId = $"{_options.ConsumerGroup}-cancellations",
            AutoOffsetReset = AutoOffsetReset.Earliest,
            EnableAutoCommit = false,
            AllowAutoCreateTopics = false
        }).Build();

        consumer.Subscribe(KafkaTopics.BookingCancelled);

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
                    _logger.LogWarning(exception, "Ошибка чтения отмены брони из Kafka");
                    continue;
                }

                BookingCancelled? message;
                try
                {
                    message = JsonSerializer.Deserialize<BookingCancelled>(
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
                    var handler = scope.ServiceProvider.GetRequiredService<IBookingCancelledHandler>();
                    var processingResult = await handler.HandleAsync(message, stoppingToken);
                    LogProcessingResult(message, processingResult);

                    if (processingResult == BookingCancellationResult.ConfirmationNotProcessed)
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
                        "Не удалось обработать отмену брони {BookingId}",
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
        BookingCancelled message,
        BookingCancellationResult result)
    {
        switch (result)
        {
            case BookingCancellationResult.Released:
                _logger.LogInformation(
                    "Для события {EventId} возвращено мест: {Seats}",
                    message.EventId,
                    message.Seats);
                break;
            case BookingCancellationResult.AlreadyProcessed:
                _logger.LogInformation(
                    "Отмена брони {BookingId} уже была обработана",
                    message.BookingId);
                break;
            case BookingCancellationResult.ConfirmationNotProcessed:
                _logger.LogWarning(
                    "Подтверждение брони {BookingId} ещё не обработано, отмена будет повторена",
                    message.BookingId);
                break;
            case BookingCancellationResult.EventNotFound:
                _logger.LogWarning(
                    "Событие {EventId} для отменённой брони {BookingId} не найдено",
                    message.EventId,
                    message.BookingId);
                break;
        }
    }
}
