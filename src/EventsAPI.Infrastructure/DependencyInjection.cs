using EventsAPI.Application.Abstractions.Persistence;
using EventsAPI.Infrastructure.Messaging;
using EventsAPI.Infrastructure.Persistence;
using EventsAPI.Infrastructure.Persistence.Repositories;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

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
        services.AddSingleton(CreateKafkaOptions(configuration));
        services.AddHostedService<KafkaTopicInitializer>();
        services.AddHostedService<BookingConfirmedConsumer>();

        return services;
    }

    private static KafkaOptions CreateKafkaOptions(IConfiguration configuration)
    {
        var section = configuration.GetSection(KafkaOptions.SectionName);
        var options = new KafkaOptions
        {
            BootstrapServers = section[nameof(KafkaOptions.BootstrapServers)] ?? string.Empty,
            ConsumerGroup = section[nameof(KafkaOptions.ConsumerGroup)] ?? string.Empty
        };

        if (string.IsNullOrWhiteSpace(options.BootstrapServers)
            || string.IsNullOrWhiteSpace(options.ConsumerGroup))
        {
            throw new InvalidOperationException("Параметры Kafka заполнены некорректно");
        }

        return options;
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
