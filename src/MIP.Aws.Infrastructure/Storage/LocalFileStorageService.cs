using MIP.Aws.Application.Abstractions.Storage;
using MIP.Aws.Application.Configuration;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace MIP.Aws.Infrastructure.Storage;

public sealed class LocalFileStorageService(IHostEnvironment host, IOptions<StorageOptions> options, ILogger<LocalFileStorageService> logger) : IFileStorageService
{
    private readonly StorageOptions _opt = options.Value;

    public string ProviderName => "local";

    public Task<Uri?> CreateReadOnlyAccessUrlAsync(string relativeLogicalPath, TimeSpan lifetime, CancellationToken cancellationToken)
    {
        var root = ResolveRoot();
        var safeRelative = relativeLogicalPath.Replace(Path.AltDirectorySeparatorChar, Path.DirectorySeparatorChar).TrimStart(Path.DirectorySeparatorChar);
        var full = Path.GetFullPath(Path.Combine(root, safeRelative));
        if (!full.StartsWith(root, StringComparison.OrdinalIgnoreCase) || !File.Exists(full))
        {
            return Task.FromResult<Uri?>(null);
        }

        // Local mode exposes a relative pseudo-URI; production deployments rely on Azure Blob SAS.
        return Task.FromResult<Uri?>(new Uri("file:///" + full.Replace('\\', '/')));
    }

    public async Task<StoredBlobDescriptor> WriteAsync(string relativeLogicalPath, byte[] content, CancellationToken cancellationToken)
    {
        var root = ResolveRoot();
        var safeRelative = relativeLogicalPath.Replace(Path.AltDirectorySeparatorChar, Path.DirectorySeparatorChar).TrimStart(Path.DirectorySeparatorChar);
        var full = Path.GetFullPath(Path.Combine(root, safeRelative));
        if (!full.StartsWith(root, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException("Invalid storage path.");
        }

        Directory.CreateDirectory(Path.GetDirectoryName(full)!);
        await File.WriteAllBytesAsync(full, content, cancellationToken).ConfigureAwait(false);
        logger.LogInformation("Stored {Bytes} bytes at {Path}", content.LongLength, full);
        return new StoredBlobDescriptor(safeRelative, full, content.LongLength);
    }

    public async Task<byte[]?> ReadAsync(string relativeLogicalPath, CancellationToken cancellationToken)
    {
        var root = ResolveRoot();
        var safeRelative = relativeLogicalPath.Replace(Path.AltDirectorySeparatorChar, Path.DirectorySeparatorChar).TrimStart(Path.DirectorySeparatorChar);
        var full = Path.GetFullPath(Path.Combine(root, safeRelative));
        if (!full.StartsWith(root, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException("Invalid storage path.");
        }

        if (!File.Exists(full))
        {
            return null;
        }

        return await File.ReadAllBytesAsync(full, cancellationToken).ConfigureAwait(false);
    }

    public async Task<int> DeleteOlderThanAsync(DateTimeOffset cutoffUtc, CancellationToken cancellationToken)
    {
        var root = ResolveRoot();
        if (!Directory.Exists(root))
        {
            return 0;
        }

        var removed = 0;
        foreach (var file in Directory.EnumerateFiles(root, "*", SearchOption.AllDirectories))
        {
            cancellationToken.ThrowIfCancellationRequested();
            var info = new FileInfo(file);
            if (info.LastWriteTimeUtc < cutoffUtc.UtcDateTime)
            {
                try
                {
                    info.Delete();
                    removed++;
                }
                catch (Exception ex)
                {
                    logger.LogWarning(ex, "Failed to delete old file {File}", file);
                }
            }
        }

        return await Task.FromResult(removed).ConfigureAwait(false);
    }

    private string ResolveRoot()
    {
        if (_opt.UseAbsoluteRoot)
        {
            return Path.GetFullPath(_opt.RootPath);
        }

        return Path.GetFullPath(Path.Combine(host.ContentRootPath, _opt.RootPath));
    }
}
