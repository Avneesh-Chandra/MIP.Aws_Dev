using MIP.Aws.Domain.Common;
using MIP.Aws.Domain.Enums;

namespace MIP.Aws.Domain.Entities.Market;

/// <summary>
/// One execution of a market-data import (manual entry, CSV upload, API pull, or scheduled job).
/// Captures full lineage so compliance can replay how a price entered the system.
/// </summary>
public class MarketDataImportJob : AuditableEntity
{
    public MarketDataProviderType Provider { get; set; }

    /// <summary>Soft label of the configuration that ran (matches <see cref="MarketDataProviderConfig.Name"/>).</summary>
    public string? ProviderConfigName { get; set; }

    public DateTimeOffset StartedAt { get; set; } = DateTimeOffset.UtcNow;

    public DateTimeOffset? CompletedAt { get; set; }

    public MarketImportStatus Status { get; set; } = MarketImportStatus.Pending;

    public int RowsAttempted { get; set; }

    public int RowsImported { get; set; }

    public int RowsSkipped { get; set; }

    public int RowsFailed { get; set; }

    public string? OriginalFileName { get; set; }

    /// <summary>Relative key into <c>IFileStorageService</c> for the uploaded CSV (when applicable).</summary>
    public string? StoredArtifactKey { get; set; }

    public string? InitiatedByEmail { get; set; }

    public Guid? InitiatedByUserId { get; set; }

    /// <summary>Top-level error message when <see cref="Status"/> is <c>Failed</c>.</summary>
    public string? ErrorMessage { get; set; }

    /// <summary>
    /// JSON array of per-row error descriptions when <see cref="Status"/> is <c>PartiallyFailed</c>:
    /// <c>[{"row":12,"symbol":"GFH","reason":"Negative close"}]</c>.
    /// </summary>
    public string? RowErrorsJson { get; set; }

    public ICollection<MarketPriceSnapshot> ImportedSnapshots { get; set; } = new List<MarketPriceSnapshot>();
}
