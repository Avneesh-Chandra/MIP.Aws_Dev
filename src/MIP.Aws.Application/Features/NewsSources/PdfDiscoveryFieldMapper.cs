using MIP.Aws.Domain.Entities;
using MIP.Aws.Domain.Enums;

namespace MIP.Aws.Application.Features.NewsSources;

public static class PdfDiscoveryFieldMapper
{
    public static PdfDiscoveryFieldsDto ToDto(NewsSource x) =>
        new(
            x.PdfDiscoveryEnabled,
            x.PdfDiscoveryMode,
            x.PdfDiscoveryPageUrl,
            x.PdfDownloadSelector,
            x.PdfLinkSelector,
            x.PdfLinkKeywords,
            x.PdfDatePattern,
            x.PreferTodayEdition,
            x.PreferLatestEdition,
            x.RequirePdfContentType,
            x.MinimumPdfSizeKb,
            x.PdfDownloadExpectedAction,
            x.PdfLinkExpectedAction,
            x.LastPdfDiscoveredAt,
            x.LastPdfDownloadedAt,
            x.LastPdfUrl,
            x.LastSavedPdfPath,
            x.AiSelectorSuggestionEnabled,
            x.PublicHtmlExtractionEnabled,
            x.GenerateInternalReportAllowed,
            x.LastPdfDiscoveryOutcome,
            x.LastPublicHtmlExtractedAt);

    public static void Apply(NewsSource entity, PdfDiscoveryFieldsDto dto)
    {
        entity.PdfDiscoveryEnabled = dto.PdfDiscoveryEnabled;
        entity.AiSelectorSuggestionEnabled = dto.AiSelectorSuggestionEnabled;
        entity.PublicHtmlExtractionEnabled = dto.PublicHtmlExtractionEnabled;
        entity.GenerateInternalReportAllowed = dto.GenerateInternalReportAllowed;
        entity.PdfDiscoveryMode = dto.PdfDiscoveryMode;
        entity.PdfDiscoveryPageUrl = dto.PdfDiscoveryPageUrl?.Trim();
        entity.PdfDownloadSelector = dto.PdfDownloadSelector?.Trim();
        entity.PdfLinkSelector = dto.PdfLinkSelector?.Trim();
        entity.PdfLinkKeywords = dto.PdfLinkKeywords?.Trim();
        entity.PdfDatePattern = dto.PdfDatePattern?.Trim();
        entity.PreferTodayEdition = dto.PreferTodayEdition;
        entity.PreferLatestEdition = dto.PreferLatestEdition;
        entity.RequirePdfContentType = dto.RequirePdfContentType;
        entity.MinimumPdfSizeKb = dto.MinimumPdfSizeKb <= 0 ? 100 : dto.MinimumPdfSizeKb;
        entity.PdfDownloadExpectedAction = dto.PdfDownloadExpectedAction;
        entity.PdfLinkExpectedAction = dto.PdfLinkExpectedAction;
    }
}
