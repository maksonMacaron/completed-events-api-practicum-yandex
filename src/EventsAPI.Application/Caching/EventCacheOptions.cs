namespace EventsAPI.Application.Caching;

public sealed class EventCacheOptions
{
    public required TimeSpan EventTimeToLive { get; init; }
    public required TimeSpan TopEventsTimeToLive { get; init; }
}
