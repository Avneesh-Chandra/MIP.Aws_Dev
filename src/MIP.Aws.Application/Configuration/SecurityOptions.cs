namespace MIP.Aws.Application.Configuration;

/// <summary>
/// Edge-layer security knobs: response headers, rate-limit windows, anti-forgery, and
/// brute-force throttling for the sign-in surface.
/// </summary>
public sealed class SecurityOptions
{
    public const string SectionName = "Security";

    public SecurityHeaderOptions Headers { get; set; } = new();

    public RateLimitOptions RateLimit { get; set; } = new();

    public LoginThrottleOptions LoginThrottle { get; set; } = new();
}

public sealed class SecurityHeaderOptions
{
    /// <summary>Master switch for response-header hardening.</summary>
    public bool Enabled { get; set; } = true;

    /// <summary>HSTS max-age in seconds; 0 disables HSTS.</summary>
    public int HstsMaxAgeSeconds { get; set; } = 31_536_000;

    /// <summary>Content Security Policy. The default is conservative and allows Blazor Server inline scripts only via nonces.</summary>
    public string ContentSecurityPolicy { get; set; } =
        "default-src 'self'; "
        + "script-src 'self' 'unsafe-inline'; "
        + "style-src 'self' 'unsafe-inline' https://fonts.googleapis.com; "
        + "img-src 'self' data: blob:; "
        + "font-src 'self' data: https://fonts.gstatic.com; "
        + "connect-src 'self' wss: https:; "
        + "frame-ancestors 'none'; "
        + "base-uri 'self'; "
        + "form-action 'self'";

    public string ReferrerPolicy { get; set; } = "strict-origin-when-cross-origin";

    public string PermissionsPolicy { get; set; } = "geolocation=(), camera=(), microphone=(), payment=()";

    public string FrameOptions { get; set; } = "DENY";

    public string XssProtection { get; set; } = "0";

    public string ContentTypeOptions { get; set; } = "nosniff";

    /// <summary>Cross-origin opener policy (defense-in-depth against spectre-style cross-window attacks).</summary>
    public string CrossOriginOpenerPolicy { get; set; } = "same-origin";

    public bool RemoveServerHeader { get; set; } = true;
}

public sealed class RateLimitOptions
{
    public bool Enabled { get; set; } = true;

    /// <summary>Default token-bucket window applied to anonymous API surface.</summary>
    public int AnonymousPermitsPerMinute { get; set; } = 60;

    /// <summary>Authenticated callers receive a larger budget.</summary>
    public int AuthenticatedPermitsPerMinute { get; set; } = 600;

    public int QueueLimit { get; set; } = 0;
}

public sealed class LoginThrottleOptions
{
    public bool Enabled { get; set; } = true;

    /// <summary>Maximum failed sign-ins per <see cref="WindowSeconds"/>.</summary>
    public int MaxFailedAttempts { get; set; } = 5;

    /// <summary>Sliding window length (seconds).</summary>
    public int WindowSeconds { get; set; } = 300;

    /// <summary>Lockout duration applied once <see cref="MaxFailedAttempts"/> is exceeded (seconds).</summary>
    public int LockoutSeconds { get; set; } = 900;
}
