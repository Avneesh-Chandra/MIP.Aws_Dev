using MIP.Aws.Application.Features.NewsSources;
using MIP.Aws.Domain.Entities;
using MIP.Aws.Domain.Security;

namespace MIP.Aws.Application.Features.SourceRecovery;

/// <summary>
/// Prevents auto-recovery from re-applying the same publisher baseline when it is already active
/// and the failure is caused by publisher/network blocking rather than configuration drift.
/// </summary>
public static class PublisherRepeatedRecoveryGuard
{
    public const string SkipSummary =
        "Known-good publisher baseline is already active. This failure is likely publisher or datacenter network blocking and cannot be fixed by re-applying the same selectors.";

    public static bool ShouldSkipRepeatedBaselineRecovery(
        NewsSource source,
        string? failureCode,
        string? errorMessage)
    {
        if (!HasKnownGoodBaselineApplied(source))
        {
            return false;
        }

        return IsNonRetriablePublisherFailure(failureCode, errorMessage);
    }

    public static bool IsRedundantBaselineOption(NewsSource source, SourceRecoveryOptionDto option) =>
        HasKnownGoodBaselineApplied(source)
        && PublisherRecoveryBaseline.IsBaselineRecoveryPatch(source.ConnectorKey, option.Patch);

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
