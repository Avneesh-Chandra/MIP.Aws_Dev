using System.Security.Cryptography;
using MIP.Aws.Application.Abstractions;
using MIP.Aws.Application.Configuration;
using MIP.Aws.Domain.Entities;

namespace MIP.Aws.Infrastructure.Portal;

internal static class PortalStoragePaths
{
    public const string PressReaderEditionFileName = "pressreader-edition.pdf";

    public static string BuildEditionRelativeDirectory(string sourceName, StorageOptions? storage = null)
    {
        var day = DateTime.UtcNow.ToString("yyyy-MM-dd", System.Globalization.CultureInfo.InvariantCulture);
        var safe = SanitizeFolderName(sourceName);
        var root = (storage?.NewspapersRelativePath ?? "newspapers").TrimEnd('/', '\\');
        return $"{root}/{safe}/{day}".Replace(Path.AltDirectorySeparatorChar, '/');
    }

    public static async Task<Guid> CreateDownloadedFileAsync(
        IApplicationDbContext db,
        Guid downloadJobId,
        string originalUrl,
        string blobUri,
        byte[] bytes,
        string contentType,
        CancellationToken cancellationToken)
    {
        var logicalKey = blobUri.Replace(Path.DirectorySeparatorChar, '/');
        var fileId = Guid.NewGuid();
        db.DownloadedFiles.Add(new DownloadedFile
        {
            Id = fileId,
            DownloadJobId = downloadJobId,
            ContentType = contentType,
            OriginalUrl = originalUrl,
            BlobUri = logicalKey,
            SizeBytes = bytes.LongLength,
            Sha256 = Convert.ToHexString(SHA256.HashData(bytes)),
            CreatedAt = DateTimeOffset.UtcNow
        });
        await db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        return fileId;
    }

    private static string SanitizeFolderName(string name)
    {
        var invalid = Path.GetInvalidFileNameChars();
        var chars = name.Select(c => invalid.Contains(c) ? '_' : c).ToArray();
        var s = new string(chars).Trim();
        return string.IsNullOrEmpty(s) ? "source" : s[..Math.Min(80, s.Length)];
    }
}
