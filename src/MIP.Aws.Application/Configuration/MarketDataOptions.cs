using MIP.Aws.Domain.Enums;

namespace MIP.Aws.Application.Configuration;

/// <summary>
/// Strongly-typed binding for the <c>MarketData</c> configuration section.
/// All values are runtime-overridable from <c>appsettings.json</c>.
/// </summary>
public sealed class MarketDataOptions
{
    public const string SectionName = "MarketData";

    /// <summary>The default provider used by scheduled jobs when no specific config is selected.</summary>
    public MarketDataProviderType DefaultProvider { get; set; } = MarketDataProviderType.ManualEntry;

    /// <summary>Provider-specific options keyed by provider name (matches <c>MarketDataProviderConfig.Name</c>).</summary>
    public Dictionary<string, MarketDataProviderEntry> Providers { get; set; } = new();

    /// <summary>Default scheduled-import expression (cron) used to register the Hangfire job.</summary>
    public string ScheduledImportCron { get; set; } = "0 17 * * MON-FRI"; // every weekday 17:00 UTC (≈ end of GCC trading)

    /// <summary>Threshold |change%| considered volatile for dashboard flagging.</summary>
    public decimal VolatilityThresholdPercent { get; set; } = 3.0m;

    /// <summary>Maximum CSV upload size accepted by the import endpoint (bytes).</summary>
    public long MaxCsvUploadBytes { get; set; } = 2 * 1024 * 1024;

    /// <summary>When true, fall back to <see cref="MarketDataProviderType.MockProvider"/> if the requested provider is unavailable.</summary>
    public bool AllowMockFallback { get; set; } = true;

    /// <summary>Opt-in AI market commentary toggle. Requires <c>Ai:Enabled=true</c> and a configured Bedrock provider.</summary>
    public bool EnableAiCommentary { get; set; } = false;

    /// <summary>Bahrain Bourse (BHB) integration settings for API and Playwright providers.</summary>
    public BahrainBourseOptions BahrainBourse { get; set; } = new();

    /// <summary>GFH multi-listing exchange page capture (Kuwait, DFM, ADX).</summary>
    public GfhSharePriceCaptureOptions GfhShareCapture { get; set; } = new();
}

/// <summary>Configuration for fetching quotes from Bahrain Bourse (bahrainbourse.com).</summary>
public sealed class BahrainBourseOptions
{
    /// <summary>When false, BahrainBourseApi provider returns an explanatory issue.</summary>
    public bool ApiEnabled { get; set; } = true;

    /// <summary>When false, BahrainBoursePlaywright provider returns an explanatory issue.</summary>
    public bool PlaywrightEnabled { get; set; } = true;

    /// <summary>
    /// Licensed vendor quote URL template. Placeholders: {symbol}, {tradeDate} (yyyy-MM-dd).
    /// When empty, the API provider attempts HTML parse of the company profile page via HTTP.
    /// </summary>
    public string? QuoteApiUrlTemplate { get; set; }

    /// <summary>Company profile page used by Playwright and HTTP fallback. Placeholder: {symbol}.</summary>
    public string CompanyProfileUrlTemplate { get; set; } =
        "https://bahrainbourse.com/en/Pages/CompanyProfile.aspx?CompanyNameSymbol={symbol}";

    /// <summary>Optional header name for API key (value from QuoteApiKey).</summary>
    public string QuoteApiKeyHeaderName { get; set; } = "X-Api-Key";

    /// <summary>API key value (prefer user-secrets / Key Vault in production).</summary>
    public string? QuoteApiKey { get; set; }

    public string DefaultExchange { get; set; } = "BHB";

    public string DefaultCurrency { get; set; } = "BHD";

    public bool PlaywrightHeadless { get; set; } = true;

    public int NavigationTimeoutMs { get; set; } = 90_000;

    public int DelayBetweenSymbolsMs { get; set; } = 1_500;

    /// <summary>Hangfire cron for <see cref="MarketDataJobs.ImportBahrainBoursePlaywrightAsync"/> (default: daily 17:00 UTC).</summary>
    public string PlaywrightImportCron { get; set; } = "0 17 * * *";

    public string UserAgent { get; set; } =
        "MIP.Aws/1.0 (market-data; +https://www.gfh.com)";
}

public sealed class MarketDataProviderEntry
{
    public MarketDataProviderType Provider { get; set; } = MarketDataProviderType.ManualEntry;

    public string? BaseUrl { get; set; }

    /// <summary>Reference to a secret in Key Vault / env vars — NEVER store the secret itself here.</summary>
    public string? AuthSecretReference { get; set; }

    public Dictionary<string, string> ExtraOptions { get; set; } = new();
}
