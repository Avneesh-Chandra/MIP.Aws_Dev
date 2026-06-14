using MIP.Aws.Domain.Entities;
using MIP.Aws.Domain.Enums;

namespace MIP.Aws.Application.Features.NewsSources;

/// <summary>
/// Source scope shared by admin/pdf-management and operator/download-monitor.
/// </summary>
public static class PdfManagementSourceRules
{
    public const string DarAlKhaleejPressReaderHost = "daralkhaleej.pressreader.com";

    /// <summary>Licensed Dar Al Khaleej PressReader editions on PDF management.</summary>
    public static bool IsLicensedDarAlKhaleejPressReader(NewsSource source) =>
        source.SourceType == NewsSourceType.WebPortalLogin
        && (source.BaseUrl.Contains(DarAlKhaleejPressReaderHost, StringComparison.OrdinalIgnoreCase)
            || (source.EditionUrl?.Contains(DarAlKhaleejPressReaderHost, StringComparison.OrdinalIgnoreCase) ?? false));

    public static bool IsLicensedDarAlKhaleejPressReader(NewsSourceListItemDto source) =>
        string.Equals(source.SourceType, NewsSourceType.WebPortalLogin.ToString(), StringComparison.Ordinal)
        && source.BaseUrl.Contains(DarAlKhaleejPressReaderHost, StringComparison.OrdinalIgnoreCase);

    /// <summary>
    /// Sources shown on /operator/download-monitor: PressReader portals plus public PDF discovery sources.
    /// </summary>
    public static bool IsPdfDownloadMonitoredSource(NewsSource source) =>
        IsLicensedDarAlKhaleejPressReader(source) || source.PdfDiscoveryEnabled;
}
