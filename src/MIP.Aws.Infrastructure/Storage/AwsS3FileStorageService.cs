using Amazon;
using Amazon.S3;
using Amazon.S3.Model;
using MIP.Aws.Application.Abstractions.Storage;
using MIP.Aws.Application.Configuration;
using MIP.Aws.Infrastructure.Aws;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace MIP.Aws.Infrastructure.Storage;

public sealed class AwsS3FileStorageService : IFileStorageService
{
    private readonly IAmazonS3 _s3;
    private readonly AwsS3Options _s3Options;
    private readonly StorageOptions _storageOptions;
    private readonly ILogger<AwsS3FileStorageService> _logger;

    public AwsS3FileStorageService(
        IAmazonS3 s3,
        IOptions<AwsOptions> awsOptions,
        IOptions<StorageOptions> storageOptions,
        ILogger<AwsS3FileStorageService> logger)
    {
        _s3 = s3;
        _s3Options = awsOptions.Value.S3;
        _storageOptions = storageOptions.Value;
        _logger = logger;
    }

    public string ProviderName => "s3";

    public async Task<StoredBlobDescriptor> WriteAsync(string relativeLogicalPath, byte[] content, CancellationToken cancellationToken)
    {
        var key = BuildObjectKey(relativeLogicalPath);
        using var stream = new MemoryStream(content);
        var request = new PutObjectRequest
        {
            BucketName = _s3Options.BucketName,
            Key = key,
            InputStream = stream,
            ContentType = InferContentType(relativeLogicalPath),
            ServerSideEncryptionMethod = ServerSideEncryptionMethod.AES256
        };

        await _s3.PutObjectAsync(request, cancellationToken).ConfigureAwait(false);
        _logger.LogInformation("Stored {Bytes} bytes in s3://{Bucket}/{Key}", content.LongLength, _s3Options.BucketName, key);
        return new StoredBlobDescriptor(relativeLogicalPath.TrimStart('/'), key, content.LongLength);
    }

    public async Task<byte[]?> ReadAsync(string relativeLogicalPath, CancellationToken cancellationToken)
    {
        var key = BuildObjectKey(relativeLogicalPath);
        try
        {
            using var response = await _s3.GetObjectAsync(_s3Options.BucketName, key, cancellationToken).ConfigureAwait(false);
            await using var ms = new MemoryStream();
            await response.ResponseStream.CopyToAsync(ms, cancellationToken).ConfigureAwait(false);
            return ms.ToArray();
        }
        catch (AmazonS3Exception ex) when (ex.StatusCode == System.Net.HttpStatusCode.NotFound)
        {
            return null;
        }
    }

    public async Task<int> DeleteOlderThanAsync(DateTimeOffset cutoffUtc, CancellationToken cancellationToken)
    {
        var removed = 0;
        var prefix = NormalizePrefix(_s3Options.Prefix);
        string? continuationToken = null;

        do
        {
            var list = await _s3.ListObjectsV2Async(new ListObjectsV2Request
            {
                BucketName = _s3Options.BucketName,
                Prefix = prefix,
                ContinuationToken = continuationToken
            }, cancellationToken).ConfigureAwait(false);

            foreach (var obj in list.S3Objects)
            {
                cancellationToken.ThrowIfCancellationRequested();
                if (obj.LastModified.ToUniversalTime() < cutoffUtc.UtcDateTime)
                {
                    await _s3.DeleteObjectAsync(_s3Options.BucketName, obj.Key, cancellationToken).ConfigureAwait(false);
                    removed++;
                }
            }

            continuationToken = list.IsTruncated ? list.NextContinuationToken : null;
        }
        while (continuationToken is not null);

        return removed;
    }

    public Task<Uri?> CreateReadOnlyAccessUrlAsync(string relativeLogicalPath, TimeSpan lifetime, CancellationToken cancellationToken)
    {
        var key = BuildObjectKey(relativeLogicalPath);
        var expiry = lifetime > TimeSpan.Zero
            ? lifetime
            : TimeSpan.FromMinutes(Math.Max(1, _storageOptions.PresignedUrlExpiryMinutes));

        var request = new GetPreSignedUrlRequest
        {
            BucketName = _s3Options.BucketName,
            Key = key,
            Verb = HttpVerb.GET,
            Expires = DateTime.UtcNow.Add(expiry)
        };

        var url = _s3.GetPreSignedURL(request);
        return Task.FromResult<Uri?>(new Uri(url));
    }

    private string BuildObjectKey(string relativeLogicalPath)
    {
        var safeRelative = relativeLogicalPath.Replace('\\', '/').TrimStart('/');
        return $"{NormalizePrefix(_s3Options.Prefix)}{safeRelative}";
    }

    private static string NormalizePrefix(string? prefix)
    {
        if (string.IsNullOrWhiteSpace(prefix))
        {
            return string.Empty;
        }

        var trimmed = prefix.Trim().Replace('\\', '/').Trim('/');
        return string.IsNullOrEmpty(trimmed) ? string.Empty : trimmed + "/";
    }

    private static string InferContentType(string path)
    {
        var ext = Path.GetExtension(path).ToLowerInvariant();
        return ext switch
        {
            ".pdf" => "application/pdf",
            ".html" or ".htm" => "text/html",
            ".png" => "image/png",
            ".jpg" or ".jpeg" => "image/jpeg",
            ".json" => "application/json",
            _ => "application/octet-stream"
        };
    }
}
