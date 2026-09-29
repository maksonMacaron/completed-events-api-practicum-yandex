using EventsAPI.Application.DTOs;
using EventsAPI.Infrastructure.Caching;
using Microsoft.Extensions.Logging.Abstractions;
using StackExchange.Redis;

namespace Services.Tests;

public sealed class RedisCacheServiceTests
{
    [Fact]
    public async Task GetAsync_ReturnsCacheMissWhenRedisIsUnavailable()
    {
        var options = new ConfigurationOptions
        {
            AbortOnConnectFail = false,
            ConnectTimeout = 100,
            AsyncTimeout = 100,
            SyncTimeout = 100
        };
        options.EndPoints.Add("127.0.0.1", 1);

        using var connection = ConnectionMultiplexer.Connect(options);
        var cache = new RedisCacheService(
            connection,
            NullLogger<RedisCacheService>.Instance);

        var result = await cache.GetAsync<EventDto>("event:unavailable-redis");

        Assert.Null(result);
    }
}
