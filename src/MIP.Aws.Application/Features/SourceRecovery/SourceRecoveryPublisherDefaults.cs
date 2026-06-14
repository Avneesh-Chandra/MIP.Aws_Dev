using MIP.Aws.Application.Features.NewsSources;
using MIP.Aws.Domain.Entities;
using MIP.Aws.Domain.Enums;

namespace MIP.Aws.Application.Features.SourceRecovery;

/// <summary>Ensures publisher-specific PDF recovery patches also enable the right discovery mode on apply.</summary>
public static class SourceRecoveryPublisherDefaults
{
    public static void ApplyAfterPatch(NewsSource source)
    {
        if (string.Equals(source.ConnectorKey, AawsatPublicPdfBaseline.ConnectorKey, StringComparison.OrdinalIgnoreCase))
        {
            source.PdfDiscoveryEnabled = true;
            source.PdfDiscoveryMode = PdfDiscoveryMode.ManualSelector;
            source.UseHeadlessBrowser = true;
            source.IsDownloadAllowed = true;
            source.RequiresManualAction = false;
            return;
        }

        if (string.Equals(source.ConnectorKey, AlAyamPublicPdfBaseline.ConnectorKey, StringComparison.OrdinalIgnoreCase))
        {
            source.PdfDiscoveryEnabled = true;
            source.PdfDiscoveryMode = PdfDiscoveryMode.ManualSelector;
            source.UseHeadlessBrowser = true;
            source.IsDownloadAllowed = true;
            source.RequiresManualAction = false;
        }
    }
}
