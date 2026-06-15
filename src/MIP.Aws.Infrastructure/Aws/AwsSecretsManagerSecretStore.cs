using Amazon.SecretsManager;
using Amazon.SecretsManager.Model;
using MIP.Aws.Application.Abstractions.Secrets;
using MIP.Aws.Infrastructure.Aws;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace MIP.Aws.Infrastructure.Aws;

public sealed class AwsSecretsManagerSecretStore : ISecretStore
{
    private readonly IAmazonSecretsManager _client;
    private readonly AwsSecretsManagerOptions _options;
    private readonly IMemoryCache _cache;
    private readonly ILogger<AwsSecretsManagerSecretStore> _logger;

    public AwsSecretsManagerSecretStore(
        IAmazonSecretsManager client,
        IOptions<AwsOptions> awsOptions,
        IMemoryCache cache,
        ILogger<AwsSecretsManagerSecretStore> logger)
    {
        _client = client;
        _options = awsOptions.Value.SecretsManager;
        _cache = cache;
        _logger = logger;
    }

    public async Task<string?> GetSecretAsync(string key, CancellationToken cancellationToken = default)
    {
        var secretId = BuildSecretId(key);
        if (_cache.TryGetValue(secretId, out string? cached))
        {
            return cached;
        }

        try
        {
            var response = await _client.GetSecretValueAsync(new GetSecretValueRequest
            {
                SecretId = secretId
            }, cancellationToken).ConfigureAwait(false);

            var value = response.SecretString;
            if (!string.IsNullOrEmpty(value))
            {
                _cache.Set(secretId, value, TimeSpan.FromMinutes(5));
            }

            return value;
        }
        catch (ResourceNotFoundException)
        {
            _logger.LogWarning("Secret not found: {SecretId}", secretId);
            return null;
        }
    }

    public async Task SetSecretAsync(string key, string value, CancellationToken cancellationToken = default)
    {
        var secretId = BuildSecretId(key);
        await _client.PutSecretValueAsync(new PutSecretValueRequest
        {
            SecretId = secretId,
            SecretString = value
        }, cancellationToken).ConfigureAwait(false);
        _cache.Set(secretId, value, TimeSpan.FromMinutes(5));
    }

    private string BuildSecretId(string key)
    {
        var prefix = (_options.Prefix ?? "mip/").TrimEnd('/');
        var normalizedKey = key.Trim().TrimStart('/');
        return $"{prefix}/{normalizedKey}";
    }
}
