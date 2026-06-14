namespace MIP.Aws.Domain.Enums;

/// <summary>Lifecycle of a market data import (manual / CSV / API).</summary>
public enum MarketImportStatus
{
    Pending = 0,
    Running = 1,
    Succeeded = 2,
    PartiallyFailed = 3,
    Failed = 4
}
