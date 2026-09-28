using BookingsAPI.Application.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace BookingsAPI.Infrastructure.BackgroundServices;

public sealed class BookingProcessingWorker : BackgroundService
{
    private static readonly TimeSpan PollingInterval = TimeSpan.FromSeconds(1);

    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<BookingProcessingWorker> _logger;

    public BookingProcessingWorker(
        IServiceScopeFactory scopeFactory,
        ILogger<BookingProcessingWorker> logger)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                IReadOnlyList<Domain.Entities.Booking> bookings;
                await using (var scope = _scopeFactory.CreateAsyncScope())
                {
                    var service = scope.ServiceProvider
                        .GetRequiredService<IBookingProcessingService>();
                    bookings = await service.GetAwaitingPublicationAsync(stoppingToken);
                }

                foreach (var booking in bookings)
                {
                    await using var scope = _scopeFactory.CreateAsyncScope();
                    var service = scope.ServiceProvider
                        .GetRequiredService<IBookingProcessingService>();

                    try
                    {
                        await service.ProcessAsync(booking, stoppingToken);
                    }
                    catch (Exception exception) when (!stoppingToken.IsCancellationRequested)
                    {
                        _logger.LogWarning(
                            exception,
                            "Не удалось опубликовать подтверждение брони {BookingId}",
                            booking.Id);
                    }
                }
            }
            catch (Exception exception) when (!stoppingToken.IsCancellationRequested)
            {
                _logger.LogError(exception, "Ошибка фоновой обработки броней");
            }

            await Task.Delay(PollingInterval, stoppingToken);
        }
    }
}
