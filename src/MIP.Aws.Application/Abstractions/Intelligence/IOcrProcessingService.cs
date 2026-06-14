namespace MIP.Aws.Application.Abstractions.Intelligence;

/// <summary>
/// OCR for legally downloaded artifacts only (PDF/HTML/images). No remote fetching.
/// </summary>
public interface IOcrProcessingService
{
    Task<OcrDocumentResult> ExtractTextAsync(byte[] content, string contentType, CancellationToken cancellationToken);
}
