using MIP.Aws.Application.Abstractions.Caching;
using MIP.Aws.Application.Abstractions.Security;
using MIP.Aws.Application.Abstractions.Telemetry;
using MIP.Aws.Application.Configuration;
using Microsoft.Extensions.Options;

namespace MIP.Aws.Infrastructure.Security;

/// <summary>
/// Login throttle backed by the shared cache (Redis in production, in-memory locally). Two counters
/// are tracked: a per-identity counter and a per-IP counter. Either one tripping the threshold
/// locks the caller out for <see cref="LoginThrottleOptions.LockoutSeconds"/>.
/// </summary>
public sealed class CacheBackedLoginThrottleService : ILoginThrottleService
{
    private const string LockSuffix = ":locked";
    private const string FailSuffix = ":failures";

    private readonly ICacheService _cache;
    private readonly LoginThrottleOptions _options;
    private readonly ITelemetryService _telemetry;

    public CacheBackedLoginThrottleService(
        ICacheService cache,
        IOptions<SecurityOptions> options,
        ITelemetryService telemetry)
    {
        _cache = cache;
        _options = options.Value.LoginThrottle;
        _telemetry = telemetry;
    }

    public async Task<LoginThrottleDecision> EvaluateAsync(string identityKey, string ipAddress, CancellationToken cancellationToken = default)
    {
        if (!_options.Enabled)
        {
            return new LoginThrottleDecision(false, 0, TimeSpan.Zero);
        }

        var identityLock = await _cache.GetAsync<LockMarker>(BuildKey(identityKey, LockSuffix), cancellationToken).ConfigureAwait(false);
        var ipLock = await _cache.GetAsync<LockMarker>(BuildKey(ipAddress, LockSuffix), cancellationToken).ConfigureAwait(false);

        var earliestUnlock = (DateTimeOffset?)null;
        if (identityLock?.UnlockAtUtc is { } i)
        {
            earliestUnlock = i;
        }

        if (ipLock?.UnlockAtUtc is { } ip)
        {
            earliestUnlock = earliestUnlock is null || ip > earliestUnlock ? ip : earliestUnlock;
        }

        if (earliestUnlock is { } unlockAt && unlockAt > DateTimeOffset.UtcNow)
        {
            _telemetry.IncrementCounter(TelemetryNames.LoginThrottled);
            return new LoginThrottleDecision(true, _options.MaxFailedAttempts, unlockAt - DateTimeOffset.UtcNow);
        }

        return new LoginThrottleDecision(false, 0, TimeSpan.Zero);
    }

    public async Task RegisterFailureAsync(string identityKey, string ipAddress, CancellationToken cancellationToken = default)
    {
        if (!_options.Enabled)
        {
            return;
        }

        var window = TimeSpan.FromSeconds(_options.WindowSeconds);
        var idFails = await _cache.IncrementAsync(BuildKey(identityKey, FailSuffix), window, cancellationToken).ConfigureAwait(false);
        var ipFails = await _cache.IncrementAsync(BuildKey(ipAddress, FailSuffix), window, cancellationToken).ConfigureAwait(false);

        if (idFails >= _options.MaxFailedAttempts || ipFails >= _options.MaxFailedAttempts)
        {
            var marker = new LockMarker { UnlockAtUtc = DateTimeOffset.UtcNow.AddSeconds(_options.LockoutSeconds) };
            var ttl = TimeSpan.FromSeconds(_options.LockoutSeconds);
            await _cache.SetAsync(BuildKey(identityKey, LockSuffix), marker, ttl, cancellationToken).ConfigureAwait(false);
            await _cache.SetAsync(BuildKey(ipAddress, LockSuffix), marker, ttl, cancellationToken).ConfigureAwait(false);
        }
    }

    public async Task RegisterSuccessAsync(string identityKey, string ipAddress, CancellationToken cancellationToken = default)
    {
        if (!_options.Enabled)
        {
            return;
        }

        await _cache.RemoveAsync(BuildKey(identityKey, FailSuffix), cancellationToken).ConfigureAwait(false);
        await _cache.RemoveAsync(BuildKey(identityKey, LockSuffix), cancellationToken).ConfigureAwait(false);
        await _cache.RemoveAsync(BuildKey(ipAddress, FailSuffix), cancellationToken).ConfigureAwait(false);
        await _cache.RemoveAsync(BuildKey(ipAddress, LockSuffix), cancellationToken).ConfigureAwait(false);
    }

    private static string BuildKey(string key, string suffix) => $"throttle:{key}{suffix}";

    private sealed class LockMarker
    {
        public DateTimeOffset UnlockAtUtc { get; set; }
    }
}
