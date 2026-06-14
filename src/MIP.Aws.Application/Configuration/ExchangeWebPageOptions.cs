namespace MIP.Aws.Application.Configuration;

/// <summary>Playwright scrape configuration for a public exchange market-watch page.</summary>
public sealed class ExchangeWebPageOptions
{
    public string ProviderKey { get; set; } = string.Empty;

    public string SourceUrl { get; set; } = string.Empty;

    public string SymbolFilter { get; set; } = "GFH";

    public string? CompanyNameContains { get; set; }

    public string DefaultExchange { get; set; } = string.Empty;

    public string DefaultCurrency { get; set; } = string.Empty;

    public int DataDelayMinutes { get; set; }

    public bool Enabled { get; set; } = true;

    public bool RequiresPlaywright { get; set; } = true;

    public bool PlaywrightHeadless { get; set; } = true;

    public int NavigationTimeoutMs { get; set; } = 90_000;

    /// <summary>
    /// When false (default), skip NetworkIdle — live market-watch pages poll continuously and often never reach idle.
    /// </summary>
    public bool WaitForNetworkIdle { get; set; }

    public int DelayAfterLoadMs { get; set; } = 2_000;

    public string? TableSelector { get; set; }

    public string? RowSelector { get; set; }

    public string? SymbolColumnSelector { get; set; }

    public string? PriceColumnSelector { get; set; }

    public string? PreviousCloseColumnSelector { get; set; }

    public string? ChangeColumnSelector { get; set; }

    public string? ChangePercentColumnSelector { get; set; }

    public string? VolumeColumnSelector { get; set; }

    public string? WaitSelector { get; set; }

    public string? SearchBoxSelector { get; set; }

    public string? CookieAcceptSelector { get; set; }

    public string UserAgent { get; set; } =
        "MIP.Aws/1.0 (market-data; +https://www.gfh.com)";
}

public sealed class GfhSharePriceCaptureOptions
{
    public const string SectionName = "GfhSharePriceCapture";

    /// <summary>Cron for weekday GFH capture before daily brief (UTC; default 05:00 UTC ≈ 08:00 Bahrain).</summary>
    public string ScheduledCaptureCron { get; set; } = "0 5 * * 1-5";

    public ExchangeWebPageOptions BoursaKuwait { get; set; } = new()
    {
        ProviderKey = "BoursaKuwait",
        SourceUrl = "https://www.boursakuwait.com.kw/en/securities/prices-and-screens/market-watch",
        SymbolFilter = "GFH",
        CompanyNameContains = "GFH Bank",
        DefaultExchange = "Boursa Kuwait",
        DefaultCurrency = "KWD",
        DataDelayMinutes = 15,
        WaitSelector = "text=Watch List",
        SearchBoxSelector = "input[placeholder*='Filter' i]",
        DelayAfterLoadMs = 10_000
    };

    public ExchangeWebPageOptions DubaiFinancialMarket { get; set; } = new()
    {
        ProviderKey = "DFM",
        SourceUrl = "https://marketwatch.dfm.ae",
        SymbolFilter = "GFH",
        CompanyNameContains = "GFH",
        DefaultExchange = "Dubai Financial Market",
        DefaultCurrency = "AED",
        TableSelector = "table",
        RowSelector = "tbody tr",
        WaitSelector = "table"
    };

    public ExchangeWebPageOptions AbuDhabiSecuritiesExchange { get; set; } = new()
    {
        ProviderKey = "ADX",
        SourceUrl = "https://www.adx.ae/all-equities",
        SymbolFilter = "GFH",
        CompanyNameContains = "GFH Bank",
        DefaultExchange = "Abu Dhabi Securities Exchange",
        DefaultCurrency = "AED",
        DataDelayMinutes = 15,
        CookieAcceptSelector = "#onetrust-accept-btn-handler, button:has-text('Accept'), button:has-text('Accept All')",
        SearchBoxSelector = "input.form-control[placeholder*='Search' i]",
        WaitSelector = ".rdt_TableRow",
        DelayAfterLoadMs = 15_000,
        WaitForNetworkIdle = false
    };
}
