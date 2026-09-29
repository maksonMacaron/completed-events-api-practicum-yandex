using BookingsAPI.Application.Abstractions.Messaging;
using BookingsAPI.Application.Abstractions.Persistence;
using BookingsAPI.Infrastructure.BackgroundServices;
using BookingsAPI.Infrastructure.Messaging;
using BookingsAPI.Infrastructure.Persistence;
using BookingsAPI.Infrastructure.Persistence.Repositories;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Shared.Contracts.Infrastructure;

namespace BookingsAPI.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructureServices(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        var connectionString = configuration.GetConnectionString("DefaultConnection");
        if (string.IsNullOrWhiteSpace(connectionString))
        {
            throw new InvalidOperationException(
                "Строка подключения ConnectionStrings:DefaultConnection не задана");
        }

        services.AddDbContext<BookingsDbContext>(options => options.UseNpgsql(connectionString));
        services.AddScoped<IBookingRepository, BookingRepository>();
        services.AddScoped<IOutboxRepository, OutboxRepository>();
        services.AddScoped<IUnitOfWork, UnitOfWork>();
        services.AddScoped<IEventCatalog, EventCatalog>();
        services.AddSingleton(KafkaOptions.FromConfiguration(configuration));
        services.AddSingleton<IBookingConfirmedPublisher, KafkaBookingConfirmedPublisher>();
        services.AddSingleton<IBookingCancelledPublisher, KafkaBookingCancelledPublisher>();
        services.AddHostedService<KafkaTopicInitializer>();
        services.AddHostedService<EventCatalogBackfillService>();
        services.AddHostedService<EventAvailabilityConsumer>();
        services.AddHostedService<KnownEventCleanupWorker>();
        services.AddHostedService<BookingProcessingWorker>();
        services.AddHostedService<OutboxPublisherWorker>();
        return services;
    }

    public static async Task ApplyInfrastructureMigrationsAsync(
        this IServiceProvider services,
        CancellationToken cancellationToken = default)
    {
        using var scope = services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<BookingsDbContext>();
        if (dbContext.Database.IsRelational())
            await dbContext.Database.MigrateAsync(cancellationToken);
    }
}
