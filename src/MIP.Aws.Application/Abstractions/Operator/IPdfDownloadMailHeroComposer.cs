namespace MIP.Aws.Application.Abstractions.Operator;

/// <summary>Builds a fanned newspaper-cover hero JPEG from PDF first pages.</summary>
public interface IPdfDownloadMailHeroComposer
{
    /// <summary>
    /// Renders the first page of each PDF (relative storage key), fans them with tilt + shadow,
    /// and returns a JPEG suitable for email CID embedding. Returns null when composition fails
    /// or fewer than one page can be rendered (caller should fall back to the static hero).
    /// </summary>
    Task<byte[]?> TryComposeJpegAsync(
        IReadOnlyList<string> pdfRelativeBlobKeys,
        CancellationToken cancellationToken = default);
}
