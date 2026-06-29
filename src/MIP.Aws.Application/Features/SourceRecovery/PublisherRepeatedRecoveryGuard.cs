using MIP.Aws.Application.Features.NewsSources;
using MIP.Aws.Domain.Entities;
using MIP.Aws.Domain.Security;

namespace MIP.Aws.Application.Features.SourceRecovery;

/// <summary>
/// Helpers for publisher baseline recovery. Baseline fields may already match the known-good
/// configuration because guards apply them before each download; auto-recovery must still run
/// the same apply-and-retry path as manual recovery in that case.
/// </summary>
public static class PublisherRepeatedRecoveryGuard
{
    public const string SkipSummary =
        "Known-good publisher baseline is already active. This failure is likely publisher or datacenter network blocking and cannot be fixed by re-applying the same selectors.";

    /// <summary>
    /// Intentionally does not skip upfront: <see cref="AlAyamPublicPdfBaseline"/> fields are often
    /// pre-applied by download guards, so manual and auto recovery share the same suggestion and
    /// apply path even when configuration already looks correct.
    /// </summary>
    public static bool ShouldSkipRepeatedBaselineRecovery(
        NewsSource source,
        string? failureCode,
        string? errorMessage) => false;

    /// <summary>
    /// Baseline publisher patches are valid auto-recovery candidates even when source fields match.
    /// </summary>
    public static bool IsRedundantBaselineOption(NewsSource source, SourceRecoveryOptionDto option) => false;

    public static bool HasKnownGoodBaselineApplied(NewsSource source)
    {
        if (AlAyamPublicPdfBaseline.IsSource(source))
        {
            return AlAyamPublicPdfBaseline.HasKnownGoodConfiguration(source);
        }

        return false;
    }

    public static bool IsNonRetriablePublisherFailure(string? failureCode, string? errorMessage)
    {
        var failureType = SourceRecoveryFailureTypeMapper.Map(failureCode, errorMessage);
        if (failureType is SourceRecoveryFailureTypes.AccessDenied
            or SourceRecoveryFailureTypes.PdfValidationFailed
            or SourceRecoveryFailureTypes.PdfLinkNotFound)
        {
            return true;
        }

        var message = errorMessage ?? string.Empty;
        return message.Contains("bot protection", StringComparison.OrdinalIgnoreCase)
               || message.Contains("AccessBlocked", StringComparison.OrdinalIgnoreCase)
               || message.Contains("datacenter egress", StringComparison.OrdinalIgnoreCase)
               || message.Contains("Publisher blocked", StringComparison.OrdinalIgnoreCase)
               || message.Contains("could not download a PDF from i.alayam.com", StringComparison.OrdinalIgnoreCase)
               || message.Contains("Response appears to be HTML", StringComparison.OrdinalIgnoreCase)
               || message.Contains("not a valid PDF", StringComparison.OrdinalIgnoreCase)
               || message.Contains("Missing PDF magic", StringComparison.OrdinalIgnoreCase)
               || (message.Contains("Al Ayam", StringComparison.OrdinalIgnoreCase)
                   && message.Contains("could not download a PDF", StringComparison.OrdinalIgnoreCase));
    }
}
