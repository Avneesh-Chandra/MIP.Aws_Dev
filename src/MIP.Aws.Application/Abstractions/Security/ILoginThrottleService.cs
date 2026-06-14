namespace MIP.Aws.Application.Abstractions.Security;

/// <summary>
/// Tracks per-identity and per-IP sign-in attempts to prevent brute-force attacks. Implementations
/// MUST persist counters in a shared store (Redis in production) so multiple API replicas agree.
/// </summary>
public interface ILoginThrottleService
{
    /// <summary>Returns true when the caller has exceeded the configured failure budget.</summary>
    Task<LoginThrottleDecision> EvaluateAsync(string identityKey, string ipAddress, CancellationToken cancellationToken = default);

    /// <summary>Increments the failure counter for the supplied identity/ip pair.</summary>
    Task RegisterFailureAsync(string identityKey, string ipAddress, CancellationToken cancellationToken = default);

    /// <summary>Clears any pending counters after a successful sign-in.</summary>
    Task RegisterSuccessAsync(string identityKey, string ipAddress, CancellationToken cancellationToken = default);
}

/// <param name="IsLocked">True when the caller is currently throttled.</param>
/// <param name="CurrentFailures">Failure count observed in the active window.</param>
/// <param name="LockoutRemaining">Time before the lockout lifts (zero when not throttled).</param>
public sealed record LoginThrottleDecision(bool IsLocked, long CurrentFailures, TimeSpan LockoutRemaining);
