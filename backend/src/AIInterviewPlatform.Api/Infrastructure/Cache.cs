using Microsoft.Extensions.Caching.Memory;

namespace AIInterviewPlatform.Api.Infrastructure;

public interface ICacheService
{
    string Kind { get; }
    Task<T?> GetAsync<T>(string key, CancellationToken ct = default);
    Task SetAsync<T>(string key, T value, TimeSpan? ttl = null, CancellationToken ct = default);
    Task RemoveAsync(string key, CancellationToken ct = default);
}

public sealed class InMemoryCacheService : ICacheService
{
    private readonly IMemoryCache _cache;

    public InMemoryCacheService(IMemoryCache cache) => _cache = cache;

    public string Kind => "Memory";

    public Task<T?> GetAsync<T>(string key, CancellationToken ct = default)
        => Task.FromResult(_cache.TryGetValue(key, out T? value) ? value : default);

    public Task SetAsync<T>(string key, T value, TimeSpan? ttl = null, CancellationToken ct = default)
    {
        _cache.Set(key, value, ttl ?? TimeSpan.FromMinutes(5));
        return Task.CompletedTask;
    }

    public Task RemoveAsync(string key, CancellationToken ct = default)
    {
        _cache.Remove(key);
        return Task.CompletedTask;
    }
}

public sealed class RedisCacheService : ICacheService
{
    private readonly StackExchange.Redis.IDatabase _db;

    public RedisCacheService(string connectionString)
    {
        var options = StackExchange.Redis.ConfigurationOptions.Parse(connectionString);
        options.AbortOnConnectFail = false;
        options.ConnectTimeout = 3000;
        options.ConnectRetry = 1;
        options.SyncTimeout = 3000;
        var conn = StackExchange.Redis.ConnectionMultiplexer.Connect(options);
        // Force a real round-trip so callers can detect a dead server and fall back.
        conn.GetDatabase().Ping();
        _db = conn.GetDatabase();
    }

    public string Kind => "Redis";

    public async Task<T?> GetAsync<T>(string key, CancellationToken ct = default)
    {
        var value = await _db.StringGetAsync(key);
        if (value.IsNullOrEmpty) return default;
        try
        {
            return System.Text.Json.JsonSerializer.Deserialize<T>(value!);
        }
        catch
        {
            return default;
        }
    }

    public async Task SetAsync<T>(string key, T value, TimeSpan? ttl = null, CancellationToken ct = default)
    {
        var json = System.Text.Json.JsonSerializer.Serialize(value);
        await _db.StringSetAsync(key, json, ttl ?? TimeSpan.FromMinutes(5));
    }

    public Task RemoveAsync(string key, CancellationToken ct = default)
        => _db.KeyDeleteAsync(key);
}