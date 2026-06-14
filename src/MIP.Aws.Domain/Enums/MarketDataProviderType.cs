namespace MIP.Aws.Domain.Enums;

/// <summary>
/// Identifies how prices arrive into the system. The platform supports manual entry, CSV upload,
/// approved-vendor APIs, and a mock provider used in dev / non-prod environments.
/// </summary>
public enum MarketDataProviderType
{
    ManualEntry = 0,
    CsvUpload = 1,
    ApprovedApi = 2,
    /// <summary>Licensed or configured HTTP feed (e.g. Bahrain Bourse vendor JSON endpoint).</summary>
    BahrainBourseApi = 3,
    /// <summary>Headless browser read of the public company profile Price tab (15-minute delayed data).</summary>
    BahrainBoursePlaywright = 4,
    /// <summary>Public market-watch page scrape — Boursa Kuwait (GFH listing).</summary>
    BoursaKuwait = 5,
    /// <summary>Public market-watch page scrape — Dubai Financial Market (GFH listing).</summary>
    DubaiFinancialMarket = 6,
    /// <summary>Public market-watch page scrape — Abu Dhabi Securities Exchange (GFH listing).</summary>
    AbuDhabiSecuritiesExchange = 7,
    MockProvider = 99
}
