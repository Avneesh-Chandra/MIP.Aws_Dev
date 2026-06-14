namespace MIP.Aws.Application.Abstractions.Caching;

/// <summary>
/// Distributed cache abstraction used by dashboards, AI result memoization, and report listings.
/// Implementations MUST treat missing entries as a clean miss (no exception) and SHOULD swallow
/// transport errors so the caller can fall back to the source of truth.
/// </summary>
public interface ICacheService
{
    /// <summary>Returns the cached value or null when the key is missing.</summary>
    Task<T?> GetAsync<T>(string key, CancellationToken cancellationToken = default) where T : class;

    /// <summary>Stores a value under the supplied key with an absolute expiration window.</summary>
    Task SetAsync<T>(string key, T value, TimeSpan? absoluteExpiration = null, CancellationToken cancellationToken = default) where T : class;

    /// <summary>Removes the specified key (no-op when missing).</summary>
    Task RemoveAsync(string key, CancellationToken cancellationToken = default);

    /// <summary>
    /// Read-through helper: returns the cached value or invokes <paramref name="factory"/> to populate it.
    /// </summary>
    Task<T> GetOrAddAsync<T>(
        string key,
        Func<CancellationToken, Task<T>> factory,
        TimeSpan? absoluteExpiration = null,
        CancellationToken cancellationToken = default) where T : class;

    /// <summary>
    /// Atomically increments a numeric counter and returns the new value. Used by the login
    /// throttling pipeline to count failed sign-ins inside a sliding window.
    /// </summary>
    Task<long> IncrementAsync(string key, TimeSpan? slidingExpiration = null, CancellationToken cancellationToken = default);
}
