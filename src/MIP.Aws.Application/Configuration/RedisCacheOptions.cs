namespace MIP.Aws.Application.Configuration;

/// <summary>
/// Distributed cache (Redis) configuration. When disabled the platform falls back to an in-process
/// memory cache so non-clustered environments remain operable.
/// </summary>
public sealed class RedisCacheOptions
{
    public const string SectionName = "RedisCache";

    /// <summary>Master switch — when false the in-memory adapter is used.</summary>
    public bool Enabled { get; set; }

    /// <summary>StackExchange.Redis-style connection string (e.g. <c>contoso.redis.cache.windows.net:6380,password=...,ssl=True</c>).</summary>
    public string? ConnectionString { get; set; }

    /// <summary>Key prefix applied to every cache entry to isolate environments.</summary>
    public string InstanceName { get; set; } = "gfh-mi:";

    /// <summary>Default TTL applied when callers don't supply one (seconds).</summary>
    public int DefaultTtlSeconds { get; set; } = 300;

    /// <summary>Dashboard metrics aggregate TTL (seconds).</summary>
    public int DashboardTtlSeconds { get; set; } = 60;

    /// <summary>AI result cache TTL (seconds). 0 disables AI caching.</summary>
    public int AiResultTtlSeconds { get; set; } = 86_400;

    /// <summary>Report listing TTL (seconds).</summary>
    public int ReportListingTtlSeconds { get; set; } = 120;
}
