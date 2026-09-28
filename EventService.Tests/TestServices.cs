using EventsAPI.Application;
using EventsAPI.Application.Abstractions.Authentication;
using EventsAPI.Application.Abstractions.Persistence;
using EventsAPI.Application.Authentication;
using EventsAPI.Infrastructure.Authentication;
using EventsAPI.Infrastructure.Persistence;
using EventsAPI.Infrastructure.Persistence.Repositories;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace EventsAPI.Tests;

internal static class TestServices
{
    public static readonly DateTimeOffset UtcNow = new(2027, 1, 15, 12, 0, 0, TimeSpan.Zero);
    public static readonly Guid UserId = Guid.Parse("11111111-1111-1111-1111-111111111111");
    public static readonly Guid OtherUserId = Guid.Parse("22222222-2222-2222-2222-222222222222");

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
        services.AddScoped<IUserRepository, UserRepository>();
        services.AddScoped<IPasswordHasher, PasswordHasher>();
        services.AddScoped<IJwtTokenGenerator, JwtTokenGenerator>();
        services.AddSingleton(new JwtSettings
        {
            Secret = "EventsApiTests_Secret_Key_With_At_Least_32_Chars",
            Issuer = "EventsAPI.Tests",
            Audience = "EventsAPI.Tests.Client",
            LifetimeMinutes = 60
        });
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
