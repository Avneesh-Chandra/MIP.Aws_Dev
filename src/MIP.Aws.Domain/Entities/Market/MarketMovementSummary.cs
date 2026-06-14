using MIP.Aws.Domain.Common;

namespace MIP.Aws.Domain.Entities.Market;

/// <summary>
/// Persisted snapshot of the daily market commentary computed by the calculation engine.
/// Cached so executive reports / emails / dashboards can render quickly without re-aggregating.
/// </summary>
/// <remarks>
/// One row per <c>(TradeDate, Scope)</c>. <see cref="Scope"/> is a free-form key like
/// <c>"global"</c>, <c>"gfh"</c>, <c>"gcc"</c>, or <c>"banking"</c> so several views can coexist.
/// </remarks>
public class MarketMovementSummary : AuditableEntity
{
    public DateOnly TradeDate { get; set; }

    public string Scope { get; set; } = "global";

    public int InstrumentsTracked { get; set; }

    public int InstrumentsWithData { get; set; }

    public int Gainers { get; set; }

    public int Losers { get; set; }

    public int Unchanged { get; set; }

    public decimal? AverageChangePercent { get; set; }

    public decimal? MaxGainPercent { get; set; }

    public string? MaxGainSymbol { get; set; }

    public decimal? MaxLossPercent { get; set; }

    public string? MaxLossSymbol { get; set; }

    public int VolatileFlags { get; set; }

    public int MissingDataCount { get; set; }

    /// <summary>JSON snapshot of the top-5 gainers / losers / most active for fast deserialization.</summary>
    public string? PayloadJson { get; set; }

    /// <summary>Optional AI commentary built strictly from stored numbers (never hallucinated).</summary>
    public string? Commentary { get; set; }

    public DateTimeOffset GeneratedAt { get; set; } = DateTimeOffset.UtcNow;
}
