using System.Xml.Linq;
using Amazon.S3;
using Amazon.S3.Model;
using Microsoft.AspNetCore.DataProtection.Repositories;
using Microsoft.Extensions.Logging;

namespace MIP.Aws.Infrastructure.Security;

/// <summary>
/// Persists ASP.NET DataProtection key ring to a private S3 prefix so ECS tasks share the same encryption keys
/// across restarts and redeploys. Portal credentials encrypted with these keys remain decryptable.
/// </summary>
public sealed class S3XmlRepository : IXmlRepository
{
    private readonly IAmazonS3 _s3;
    private readonly string _bucket;
    private readonly string _prefix;
    private readonly ILogger<S3XmlRepository> _logger;

    public S3XmlRepository(
        IAmazonS3 s3,
        string bucket,
        string prefix,
        ILogger<S3XmlRepository> logger)
    {
        _s3 = s3;
        _bucket = bucket;
        _prefix = prefix.TrimEnd('/') + "/";
        _logger = logger;
    }

    public string BucketName => _bucket;

    public string Prefix => _prefix;

    public IReadOnlyCollection<XElement> GetAllElements()
    {
        var elements = new List<XElement>();
        var request = new ListObjectsV2Request
        {
            BucketName = _bucket,
            Prefix = _prefix
        };

        try
        {
            ListObjectsV2Response response;
            do
            {
                response = _s3.ListObjectsV2Async(request).GetAwaiter().GetResult();
                foreach (var obj in response.S3Objects)
                {
                    if (!obj.Key.EndsWith(".xml", StringComparison.OrdinalIgnoreCase))
                    {
                        continue;
                    }

                    using var get = _s3.GetObjectAsync(_bucket, obj.Key).GetAwaiter().GetResult();
                    using var reader = new StreamReader(get.ResponseStream);
                    elements.Add(XElement.Parse(reader.ReadToEnd()));
                }

                request.ContinuationToken = response.NextContinuationToken;
            }
            while (response.IsTruncated);
        }
        catch (AmazonS3Exception ex)
        {
            // Returning an empty key ring would cause ASP.NET to mint NEW keys and permanently
            // orphan every previously encrypted portal password. Fail loudly instead.
            _logger.LogError(
                ex,
                "Failed to load DataProtection keys from s3://{Bucket}/{Prefix} (ErrorCode={ErrorCode}). Portal credentials cannot be decrypted until S3 access is fixed.",
                _bucket,
                _prefix,
                ex.ErrorCode);
            throw new InvalidOperationException(
                $"DataProtection key ring could not be loaded from s3://{_bucket}/{_prefix}. " +
                "Fix ECS task-role S3 ListBucket/GetObject permissions before continuing.",
                ex);
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                "Unexpected failure loading DataProtection keys from s3://{Bucket}/{Prefix}.",
                _bucket,
                _prefix);
            throw new InvalidOperationException(
                $"DataProtection key ring could not be loaded from s3://{_bucket}/{_prefix}.",
                ex);
        }

        _logger.LogInformation(
            "Loaded {Count} DataProtection key(s) from s3://{Bucket}/{Prefix}.",
            elements.Count,
            _bucket,
            _prefix);
        return elements;
    }

    public void StoreElement(XElement element, string friendlyName)
    {
        var key = _prefix + friendlyName + ".xml";
        try
        {
            _s3.PutObjectAsync(new PutObjectRequest
            {
                BucketName = _bucket,
                Key = key,
                ContentBody = element.ToString(SaveOptions.DisableFormatting),
                ContentType = "application/xml",
                ServerSideEncryptionMethod = ServerSideEncryptionMethod.AES256
            }).GetAwaiter().GetResult();
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                "Failed to store DataProtection key {FriendlyName} to s3://{Bucket}/{Key}.",
                friendlyName,
                _bucket,
                key);
            throw;
        }

        _logger.LogInformation(
            "Stored DataProtection key {FriendlyName} to s3://{Bucket}/{Key}",
            friendlyName,
            _bucket,
            key);
    }
}
