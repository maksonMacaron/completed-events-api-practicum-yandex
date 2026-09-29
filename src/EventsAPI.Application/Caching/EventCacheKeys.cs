namespace EventsAPI.Application.Caching;

public static class EventCacheKeys
{
    public const string TopEvents = "events:top10";

    public static string ById(Guid id) => $"event:{id}";
}
