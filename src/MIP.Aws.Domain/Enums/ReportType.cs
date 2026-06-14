namespace MIP.Aws.Domain.Enums;

public enum ReportType
{
    ExecutiveSummary = 1,
    Analyst = 2,
    MarketIntelligence = 3,
    DailyStock = 4,
    Compliance = 5,
    /// <summary>Daily consolidated intelligence digest.</summary>
    DailyIntelligence = 6,
    /// <summary>GFH and subsidiary mention digest.</summary>
    GfhMention = 7
}
