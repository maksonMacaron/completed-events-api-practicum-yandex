using EventsAPI.Application.Abstractions.Messaging;
using EventsAPI.Application.Abstractions.Persistence;
using EventsAPI.Infrastructure.Messaging;
using EventsAPI.Infrastructure.Persistence;
using EventsAPI.Infrastructure.Persistence.Repositories;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Shared.Contracts.Infrastructure;

namespace EventsAPI.Infrastructure;

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

        services.AddDbContext<AppDbContext>(options =>
            options.UseNpgsql(connectionString));
        services.AddScoped<IEventRepository, EventRepository>();
        services.AddSingleton(KafkaOptions.FromConfiguration(configuration));
        services.AddSingleton<IEventAvailabilityPublisher, KafkaEventAvailabilityPublisher>();
        services.AddHostedService<KafkaTopicInitializer>();
        services.AddHostedService<EventCatalogInitializer>();
        services.AddHostedService<BookingConfirmedConsumer>();
        services.AddHostedService<BookingCancelledConsumer>();

        return services;
    }

    public static async Task ApplyInfrastructureMigrationsAsync(
        this IServiceProvider services,
        CancellationToken cancellationToken = default)
    {
        using var scope = services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        if (dbContext.Database.IsRelational())
            await dbContext.Database.MigrateAsync(cancellationToken);
    }
}
