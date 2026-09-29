using System.Diagnostics;
using System.Text.Json;
using BookingsAPI.Application.Abstractions.Persistence;
using Confluent.Kafka;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Shared.Contracts;
using Shared.Contracts.Infrastructure;

namespace BookingsAPI.Infrastructure.BackgroundServices;

public sealed class EventCatalogBackfillService : IHostedService
{
    private static readonly TimeSpan ConsumeTimeout = TimeSpan.FromMilliseconds(500);
    private static readonly TimeSpan IdleTimeout = TimeSpan.FromSeconds(1);
    private static readonly TimeSpan MaximumDuration = TimeSpan.FromSeconds(30);

    private readonly KafkaOptions _options;
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<EventCatalogBackfillService> _logger;

    public EventCatalogBackfillService(
        KafkaOptions options,
        IServiceScopeFactory scopeFactory,
        ILogger<EventCatalogBackfillService> logger)
    {
        _options = options;
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        using var consumer = new ConsumerBuilder<string, string>(new ConsumerConfig
        {
            BootstrapServers = _options.BootstrapServers,
            GroupId = $"{_options.ConsumerGroup}-catalog-backfill",
            AutoOffsetReset = AutoOffsetReset.Earliest,
            EnableAutoCommit = false,
            AllowAutoCreateTopics = false
        }).Build();

        consumer.Subscribe(KafkaTopics.EventAvailabilityChanged);
        var stopwatch = Stopwatch.StartNew();
        var idleSince = stopwatch.Elapsed;
        var processedCount = 0;

        try
        {
            while (stopwatch.Elapsed < MaximumDuration && !cancellationToken.IsCancellationRequested)
            {
                var result = consumer.Consume(ConsumeTimeout);
                if (result is null)
                {
                    if (consumer.Assignment.Count > 0
                        && stopwatch.Elapsed - idleSince >= IdleTimeout)
                    {
                        break;
                    }

                    continue;
                }

                idleSince = stopwatch.Elapsed;
                var message = JsonSerializer.Deserialize<EventAvailabilityChanged>(
                    result.Message.Value,
                    KafkaJsonSerializer.Options);
                if (message is null)
                    continue;

                await using var scope = _scopeFactory.CreateAsyncScope();
                var eventCatalog = scope.ServiceProvider.GetRequiredService<IEventCatalog>();
                await eventCatalog.ApplyAsync(
                    message.EventId,
                    message.IsAvailable,
                    message.ChangedAt,
                    cancellationToken);
                processedCount++;
            }

            _logger.LogInformation(
                "Начальная синхронизация каталога завершена, обработано сообщений: {MessageCount}",
                processedCount);
        }
        catch (Exception exception) when (!cancellationToken.IsCancellationRequested)
        {
            _logger.LogWarning(exception, "Не удалось выполнить начальную синхронизацию каталога");
        }
        finally
        {
            consumer.Close();
        }
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}
