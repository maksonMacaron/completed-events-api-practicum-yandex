using EventsAPI.Application.Services;
using Microsoft.Extensions.DependencyInjection;

namespace EventsAPI.Application;

public static class DependencyInjection
{
    public static IServiceCollection AddApplicationServices(this IServiceCollection services)
    {
        services.AddScoped<IEventService, EventService>();
        services.AddScoped<IBookingService, BookingService>();
        services.AddScoped<IBookingProcessingService, BookingProcessingService>();
        return services;
    }
}
