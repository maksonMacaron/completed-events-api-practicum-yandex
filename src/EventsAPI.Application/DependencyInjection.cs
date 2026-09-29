using EventsAPI.Application.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace EventsAPI.Application;

public static class DependencyInjection
{
    public static IServiceCollection AddApplicationServices(this IServiceCollection services)
    {
        services.TryAddSingleton(TimeProvider.System);
        services.AddScoped<IEventService, EventService>();
        services.AddScoped<IBookingConfirmedHandler, BookingConfirmedHandler>();
        services.AddScoped<IBookingCancelledHandler, BookingCancelledHandler>();
        return services;
    }
}
