namespace EventsAPI.Application.Abstractions.Caching;

public interface ICacheService
{
    Task<T?> GetAsync<T>(
        string key,
        CancellationToken cancellationToken = default)
        where T : class;

    Task SetAsync<T>(
        string key,
        T value,
        TimeSpan timeToLive,
        CancellationToken cancellationToken = default)
        where T : class;

    Task RemoveAsync(
        string key,
        CancellationToken cancellationToken = default);
}
