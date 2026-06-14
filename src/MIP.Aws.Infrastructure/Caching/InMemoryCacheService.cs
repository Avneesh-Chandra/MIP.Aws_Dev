using System.Text.Json;
using MIP.Aws.Application.Abstractions.Caching;
using MIP.Aws.Application.Abstractions.Telemetry;
using MIP.Aws.Application.Configuration;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Options;

namespace MIP.Aws.Infrastructure.Caching;

public sealed class InMemoryCacheService(
    IMemoryCache memoryCache,
    IOptions<RedisCacheOptions> options,
    ITelemetryService telemetry) : ICacheService
{
    private readonly RedisCacheOptions _options = options.Value;

    public Task<T?> GetAsync<T>(string key, CancellationToken cancellationToken = default) where T : class
    {
        if (memoryCache.TryGetValue(BuildKey(key), out var raw) && raw is string json)
        {
            telemetry.IncrementCounter(TelemetryNames.CacheHit, tags: new Dictionary<string, object?> { ["provider"] = "memory" });
            return Task.FromResult<T?>(JsonSerializer.Deserialize<T>(json));
        }

        telemetry.IncrementCounter(TelemetryNames.CacheMiss, tags: new Dictionary<string, object?> { ["provider"] = "memory" });
        return Task.FromResult<T?>(null);
    }

    public Task SetAsync<T>(string key, T value, TimeSpan? absoluteExpiration = null, CancellationToken cancellationToken = default) where T : class
    {
        var ttl = absoluteExpiration ?? TimeSpan.FromSeconds(_options.DefaultTtlSeconds);
        memoryCache.Set(BuildKey(key), JsonSerializer.Serialize(value), ttl);
        return Task.CompletedTask;
    }

    public Task RemoveAsync(string key, CancellationToken cancellationToken = default)
    {
        memoryCache.Remove(BuildKey(key));
        return Task.CompletedTask;
    }

    public async Task<T> GetOrAddAsync<T>(string key, Func<CancellationToken, Task<T>> factory, TimeSpan? absoluteExpiration = null, CancellationToken cancellationToken = default) where T : class
    {
        var existing = await GetAsync<T>(key, cancellationToken).ConfigureAwait(false);
        if (existing is not null)
        {
            return existing;
        }

        var produced = await factory(cancellationToken).ConfigureAwait(false);
        if (produced is not null)
        {
            await SetAsync(key, produced, absoluteExpiration, cancellationToken).ConfigureAwait(false);
        }

        return produced!;
    }

    public Task<long> IncrementAsync(string key, TimeSpan? slidingExpiration = null, CancellationToken cancellationToken = default)
    {
        var bucketKey = BuildKey(key);
        var ttl = slidingExpiration ?? TimeSpan.FromSeconds(_options.DefaultTtlSeconds);
        var entry = memoryCache.GetOrCreate(bucketKey, e =>
        {
            e.AbsoluteExpirationRelativeToNow = ttl;
            return new Counter();
        }) ?? new Counter();

        return Task.FromResult(Interlocked.Increment(ref entry.Value));
    }

    private string BuildKey(string key) =>
        string.IsNullOrEmpty(_options.InstanceName) ? key : _options.InstanceName + key;

    private sealed class Counter
    {
        public long Value;
    }
}
