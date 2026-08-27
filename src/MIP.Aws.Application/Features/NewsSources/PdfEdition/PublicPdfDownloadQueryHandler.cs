using System.IO.Compression;
using MIP.Aws.Application.Abstractions;
using MIP.Aws.Application.Abstractions.Operator;
using MIP.Aws.Application.Abstractions.Storage;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace MIP.Aws.Application.Features.NewsSources.PdfEdition;

public sealed class StreamPublicPdfDownloadQueryHandler(
    IApplicationDbContext db,
    IFileStorageService fileStorage,
    IPdfDownloadMailLinkService linkService)
    : IRequestHandler<StreamPublicPdfDownloadQuery, PdfEditionStreamResult?>
{
    public async Task<PdfEditionStreamResult?> Handle(
        StreamPublicPdfDownloadQuery request,
        CancellationToken cancellationToken)
    {
        if (!linkService.TryReadToken(request.Token, out var payload)
            || payload?.Files is not { Count: > 0 and <= 100 })
        {
            return null;
        }

        var references = payload.Files
            .DistinctBy(file => file.FileId)
            .ToList();
        if (references.Count != payload.Files.Count)
        {
            return null;
        }

        var fileIds = references.Select(file => file.FileId).ToList();
        var storedFiles = await db.DownloadedFiles.AsNoTracking()
            .Where(file => !file.IsDeleted
                           && !file.DownloadJob!.IsDeleted
                           && !file.DownloadJob.NewsSource.IsDeleted
                           && fileIds.Contains(file.Id))
            .Select(file => new StoredPdf(
                file.Id,
                file.DownloadJob!.NewsSourceId,
                file.ContentType,
                file.BlobUri))
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        if (storedFiles.Count != references.Count
            || storedFiles.Any(file => !IsPdf(file.ContentType)
                                       || !references.Any(reference =>
                                           reference.FileId == file.Id
                                           && reference.SourceId == file.SourceId)))
        {
            return null;
        }

        var content = new List<DownloadedPdf>(storedFiles.Count);
        foreach (var reference in references)
        {
            var stored = storedFiles.First(file => file.Id == reference.FileId);
            var bytes = await fileStorage.ReadAsync(stored.BlobUri, cancellationToken).ConfigureAwait(false);
            if (bytes is null || bytes.Length == 0)
            {
                return null;
            }

            content.Add(new DownloadedPdf(
                bytes,
                SanitizeFileName(reference.FileName, reference.FileId)));
        }

        if (content.Count == 1)
        {
            return new PdfEditionStreamResult(
                content[0].Bytes,
                "application/pdf",
                content[0].FileName);
        }

        await using var output = new MemoryStream();
        using (var archive = new ZipArchive(output, ZipArchiveMode.Create, leaveOpen: true))
        {
            var usedNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var pdf in content)
            {
                var fileName = MakeUniqueFileName(pdf.FileName, usedNames);
                var entry = archive.CreateEntry(fileName, CompressionLevel.Fastest);
                await using var entryStream = entry.Open();
                await entryStream.WriteAsync(pdf.Bytes, cancellationToken).ConfigureAwait(false);
            }
        }

        return new PdfEditionStreamResult(output.ToArray(), "application/zip", "gfh-pdf-downloads.zip");
    }

    private static bool IsPdf(string? contentType) =>
        string.Equals(contentType, "application/pdf", StringComparison.OrdinalIgnoreCase);

    private static string SanitizeFileName(string? fileName, Guid fileId)
    {
        var safeName = Path.GetFileName(fileName?.Replace('\\', '/') ?? string.Empty);
        safeName = new string(
            safeName
                .Where(character => !char.IsControl(character) && character is not '"' and not '\r' and not '\n')
                .ToArray());
        if (string.IsNullOrWhiteSpace(safeName))
        {
            safeName = $"pdf-{fileId:N}.pdf";
        }

        return safeName.EndsWith(".pdf", StringComparison.OrdinalIgnoreCase)
            ? safeName
            : $"{safeName}.pdf";
    }

    private static string MakeUniqueFileName(string fileName, ISet<string> usedNames)
    {
        if (usedNames.Add(fileName))
        {
            return fileName;
        }

        var stem = Path.GetFileNameWithoutExtension(fileName);
        var extension = Path.GetExtension(fileName);
        var suffix = 2;
        var candidate = $"{stem}-{suffix}{extension}";
        while (!usedNames.Add(candidate))
        {
            suffix++;
            candidate = $"{stem}-{suffix}{extension}";
        }

        return candidate;
    }

    private sealed record StoredPdf(Guid Id, Guid SourceId, string ContentType, string BlobUri);

    private sealed record DownloadedPdf(byte[] Bytes, string FileName);
}
