using System.Text.Json;
using EventsAPI.Application.Abstractions.Caching;
using Microsoft.Extensions.Logging;
using StackExchange.Redis;

namespace EventsAPI.Infrastructure.Caching;

public sealed class RedisCacheService : ICacheService
{
    private static readonly JsonSerializerOptions SerializerOptions =
        new(JsonSerializerDefaults.Web);

    private readonly IDatabase _database;
    private readonly ILogger<RedisCacheService> _logger;

    public RedisCacheService(
        IConnectionMultiplexer connection,
        ILogger<RedisCacheService> logger)
    {
        _database = connection.GetDatabase();
        _logger = logger;
    }

    public async Task<T?> GetAsync<T>(
        string key,
        CancellationToken cancellationToken = default)
        where T : class
    {
        cancellationToken.ThrowIfCancellationRequested();

        try
        {
            var value = await _database.StringGetAsync(key);
            if (!value.HasValue)
                return null;

            return JsonSerializer.Deserialize<T>(value.ToString(), SerializerOptions);
        }
        catch (Exception exception) when (exception is RedisException or TimeoutException)
        {
            _logger.LogWarning(exception, "Не удалось прочитать ключ {CacheKey} из Redis", key);
            return null;
        }
        catch (JsonException exception)
        {
            _logger.LogWarning(exception, "В Redis сохранено некорректное значение ключа {CacheKey}", key);
            return null;
        }
    }

    public async Task SetAsync<T>(
        string key,
        T value,
        TimeSpan timeToLive,
        CancellationToken cancellationToken = default)
        where T : class
    {
        cancellationToken.ThrowIfCancellationRequested();

        try
        {
            var json = JsonSerializer.Serialize(value, SerializerOptions);
            await _database.StringSetAsync(key, json, timeToLive);
        }
        catch (Exception exception) when (exception is RedisException or TimeoutException)
        {
            _logger.LogWarning(exception, "Не удалось записать ключ {CacheKey} в Redis", key);
        }
    }

    public async Task RemoveAsync(
        string key,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        try
        {
            await _database.KeyDeleteAsync(key);
        }
        catch (Exception exception) when (exception is RedisException or TimeoutException)
        {
            _logger.LogWarning(exception, "Не удалось удалить ключ {CacheKey} из Redis", key);
        }
    }
}
