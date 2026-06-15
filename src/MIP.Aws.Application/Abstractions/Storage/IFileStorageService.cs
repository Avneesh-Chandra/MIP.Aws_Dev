namespace MIP.Aws.Application.Abstractions.Storage;

public interface IFileStorageService
{
    Task<StoredBlobDescriptor> WriteAsync(string relativeLogicalPath, byte[] content, CancellationToken cancellationToken);
    Task<byte[]?> ReadAsync(string relativeLogicalPath, CancellationToken cancellationToken);
    Task<int> DeleteOlderThanAsync(DateTimeOffset cutoffUtc, CancellationToken cancellationToken);
    Task<Uri?> CreateReadOnlyAccessUrlAsync(string relativeLogicalPath, TimeSpan lifetime, CancellationToken cancellationToken);
    string ProviderName { get; }
}

public sealed record StoredBlobDescriptor(string RelativeKey, string FullPhysicalPath, long SizeBytes);
