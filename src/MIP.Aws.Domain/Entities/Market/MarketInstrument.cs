using MIP.Aws.Domain.Common;
using MIP.Aws.Domain.Enums;

namespace MIP.Aws.Domain.Entities.Market;

/// <summary>
/// A tradeable instrument tracked by the Market Intelligence module — an equity (GFH BHK),
/// an index (BAX, TADAWUL ALL SHARE), an FX pair, a commodity, a fund, or a bond.
/// </summary>
/// <remarks>
/// Symbols are unique <em>per exchange</em> (e.g. "GFH" on the Bahrain Bourse and "GFH" on
/// a hypothetical second venue would be two rows). Prices are stored in <see cref="MarketPriceSnapshot"/>.
/// </remarks>
public class MarketInstrument : AuditableEntity
{
    public string Symbol { get; set; } = string.Empty;

    public string Name { get; set; } = string.Empty;

    public string Exchange { get; set; } = string.Empty;

    /// <summary>ISO 3166-1 alpha-2 country code (e.g. BH, SA, AE, KW, OM, QA, US).</summary>
    public string Country { get; set; } = string.Empty;

    /// <summary>ISO 4217 currency code (e.g. BHD, USD, SAR).</summary>
    public string Currency { get; set; } = string.Empty;

    public MarketInstrumentType InstrumentType { get; set; } = MarketInstrumentType.Equity;

    /// <summary>Optional sector classification (e.g. Banking, Financial Services, Energy).</summary>
    public string? Sector { get; set; }

    /// <summary>Free-form notes / disclosure / data-source memo (analyst only).</summary>
    public string? Notes { get; set; }

    public bool IsActive { get; set; } = true;

    /// <summary>Sort hint inside the dashboard widgets.</summary>
    public int DisplayOrder { get; set; }

    /// <summary>When true, this instrument is promoted into executive reports / briefs / email digests.</summary>
    public bool IsFeaturedForExecutiveReport { get; set; }

    /// <summary>Quick flag identifying GFH's own equity — used by GFH-specific dashboards.</summary>
    public bool IsGfhStock { get; set; }

    public ICollection<MarketPriceSnapshot> Snapshots { get; set; } = new List<MarketPriceSnapshot>();
}
