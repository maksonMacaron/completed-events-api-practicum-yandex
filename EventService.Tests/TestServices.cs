using EventsAPI.Application;
using EventsAPI.Application.Abstractions.Persistence;
using EventsAPI.Infrastructure.Persistence;
using EventsAPI.Infrastructure.Persistence.Repositories;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace EventsAPI.Tests;

internal static class TestServices
{
    public static readonly DateTimeOffset UtcNow = new(2027, 1, 15, 12, 0, 0, TimeSpan.Zero);

    public static ServiceProvider BuildProvider()
    {
        var dbName = Guid.NewGuid().ToString();
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddSingleton<TimeProvider>(new FixedTimeProvider(UtcNow));
        services.AddDbContext<AppDbContext>(options =>
            options.UseInMemoryDatabase(dbName));
        services.AddScoped<IEventRepository, EventRepository>();
        services.AddScoped<IBookingRepository, BookingRepository>();
        services.AddApplicationServices();

        return services.BuildServiceProvider(validateScopes: true);
    }

    private sealed class FixedTimeProvider : TimeProvider
    {
        private readonly DateTimeOffset _utcNow;

        public FixedTimeProvider(DateTimeOffset utcNow)
        {
            _utcNow = utcNow;
        }

        public override DateTimeOffset GetUtcNow() => _utcNow;
    }
}
