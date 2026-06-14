namespace MIP.Aws.Application.Abstractions.Intelligence;

public sealed record SegmentedArticleDraft(
    string Headline,
    string Body,
    int StartPage,
    int EndPage,
    string LanguageGuess);

/// <summary>
/// Splits OCR page text into candidate articles (heuristic + Arabic/English safe).
/// </summary>
public interface IArticleSegmentationService
{
    Task<IReadOnlyList<SegmentedArticleDraft>> SegmentAsync(IReadOnlyList<OcrPageDto> pages, CancellationToken cancellationToken);
}
