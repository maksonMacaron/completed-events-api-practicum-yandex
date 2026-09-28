using BookingsAPI.Application.Abstractions.Messaging;
using BookingsAPI.Application.Abstractions.Persistence;
using BookingsAPI.Infrastructure.BackgroundServices;
using BookingsAPI.Infrastructure.Messaging;
using BookingsAPI.Infrastructure.Persistence;
using BookingsAPI.Infrastructure.Persistence.Repositories;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

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
        services.AddSingleton(CreateKafkaOptions(configuration));
        services.AddSingleton<IBookingConfirmedPublisher, KafkaBookingConfirmedPublisher>();
        services.AddHostedService<BookingProcessingWorker>();
        return services;
    }

    private static KafkaOptions CreateKafkaOptions(IConfiguration configuration)
    {
        var section = configuration.GetSection(KafkaOptions.SectionName);
        var options = new KafkaOptions
        {
            BootstrapServers = section[nameof(KafkaOptions.BootstrapServers)] ?? string.Empty
        };

        if (string.IsNullOrWhiteSpace(options.BootstrapServers))
            throw new InvalidOperationException("Адрес Kafka не задан");

        return options;
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
