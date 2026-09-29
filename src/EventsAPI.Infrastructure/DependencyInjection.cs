using EventsAPI.Application.Abstractions.Caching;
using EventsAPI.Application.Abstractions.Messaging;
using EventsAPI.Application.Abstractions.Persistence;
using EventsAPI.Application.Caching;
using EventsAPI.Infrastructure.Caching;
using EventsAPI.Infrastructure.Messaging;
using EventsAPI.Infrastructure.Persistence;
using EventsAPI.Infrastructure.Persistence.Repositories;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Shared.Contracts.Infrastructure;
using StackExchange.Redis;

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
        AddRedisCache(services, configuration);
        services.AddSingleton(KafkaOptions.FromConfiguration(configuration));
        services.AddSingleton<IEventAvailabilityPublisher, KafkaEventAvailabilityPublisher>();
        services.AddHostedService<KafkaTopicInitializer>();
        services.AddHostedService<EventCatalogInitializer>();
        services.AddHostedService<BookingConfirmedConsumer>();
        services.AddHostedService<BookingCancelledConsumer>();

        return services;
    }

    private static void AddRedisCache(
        IServiceCollection services,
        IConfiguration configuration)
    {
        var section = configuration.GetSection("Redis");
        var redisConnectionString = section["ConnectionString"];
        var eventTtlIsValid = int.TryParse(section["EventTtlMinutes"], out var eventTtlMinutes);
        var topEventsTtlIsValid = int.TryParse(
            section["TopEventsTtlMinutes"],
            out var topEventsTtlMinutes);

        if (string.IsNullOrWhiteSpace(redisConnectionString)
            || !eventTtlIsValid
            || eventTtlMinutes <= 0
            || !topEventsTtlIsValid
            || topEventsTtlMinutes <= 0)
        {
            throw new InvalidOperationException(
                "Параметры Redis в секции Redis заполнены некорректно");
        }

        var redisConfiguration = ConfigurationOptions.Parse(redisConnectionString);
        redisConfiguration.AbortOnConnectFail = false;

        services.AddSingleton(new EventCacheOptions
        {
            EventTimeToLive = TimeSpan.FromMinutes(eventTtlMinutes),
            TopEventsTimeToLive = TimeSpan.FromMinutes(topEventsTtlMinutes)
        });
        services.AddSingleton<IConnectionMultiplexer>(
            _ => ConnectionMultiplexer.Connect(redisConfiguration));
        services.AddSingleton<ICacheService, RedisCacheService>();
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
