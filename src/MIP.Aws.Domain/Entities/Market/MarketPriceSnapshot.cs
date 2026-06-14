using MIP.Aws.Domain.Common;
using MIP.Aws.Domain.Enums;

namespace MIP.Aws.Domain.Entities.Market;

/// <summary>
/// A single trade-day OHLCV record for a <see cref="MarketInstrument"/>, sourced from a single provider.
/// </summary>
/// <remarks>
/// All decimal columns are stored at <c>decimal(18, 6)</c> precision so the platform can carry
/// micro-tick FX quotes alongside whole-number index levels without rounding.
/// Uniqueness is enforced on <c>(InstrumentId, TradeDate, Provider)</c> — letting analysts compare
/// the same instrument across providers if/when GFH connects to an approved vendor.
/// </remarks>
public class MarketPriceSnapshot : AuditableEntity
{
    public Guid InstrumentId { get; set; }

    public MarketInstrument? Instrument { get; set; }

    public DateOnly TradeDate { get; set; }

    public decimal Open { get; set; }

    public decimal High { get; set; }

    public decimal Low { get; set; }

    public decimal Close { get; set; }

    public decimal? PreviousClose { get; set; }

    /// <summary>Absolute change vs. previous close (close − previousClose).</summary>
    public decimal? Change { get; set; }

    /// <summary>Percentage change vs. previous close, expressed as a percent (e.g. 1.25 means +1.25%).</summary>
    public decimal? ChangePercent { get; set; }

    public decimal? Volume { get; set; }

    /// <summary>ISO 4217 currency code, copied from the instrument at import time so historical rows remain stable.</summary>
    public string Currency { get; set; } = string.Empty;

    public MarketDataProviderType SourceProvider { get; set; } = MarketDataProviderType.ManualEntry;

    /// <summary>Optional human-readable provider tag — e.g. "Bahrain Bourse Daily Bulletin 2026-05-12".</summary>
    public string? SourceReference { get; set; }

    public DateTimeOffset ImportedAt { get; set; } = DateTimeOffset.UtcNow;

    /// <summary>Tracks the originating <see cref="MarketDataImportJob"/> when imported in bulk.</summary>
    public Guid? ImportJobId { get; set; }

    public MarketDataImportJob? ImportJob { get; set; }

    /// <summary>
    /// True when the calculation engine flags this row as volatile relative to recent history
    /// (e.g. |change%| above the configured 3-sigma threshold).
    /// </summary>
    public bool IsVolatile { get; set; }

    /// <summary>Free-form analyst memo (rare — manual edits only).</summary>
    public string? AnalystNote { get; set; }

    /// <summary>Public page URL used for this capture (exchange web providers).</summary>
    public string? SourceUrl { get; set; }

    /// <summary>Raw DOM/table payload for audit and troubleshooting (JSON).</summary>
    public string? RawPayloadJson { get; set; }

    /// <summary>Exchange-reported delay in minutes (e.g. ADX 15-minute delay).</summary>
    public int? DataDelayMinutes { get; set; }

    /// <summary>Market session status at capture (Open, Closed, Close-Of-Day).</summary>
    public string? MarketStatus { get; set; }

    /// <summary>When the price was captured from the source (may differ from <see cref="ImportedAt"/>).</summary>
    public DateTimeOffset? CapturedAt { get; set; }

    /// <summary>Notes from fils/currency normalization or other transforms.</summary>
    public string? NormalizationNotes { get; set; }
}
