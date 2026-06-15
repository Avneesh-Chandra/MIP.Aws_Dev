using Amazon;
using Amazon.BedrockRuntime;
using Amazon.Runtime.CredentialManagement;
using Microsoft.Extensions.Options;

namespace MIP.Aws.Infrastructure.Aws;

/// <summary>Creates Bedrock runtime clients using the AWS SDK default credential chain and optional CLI profile.</summary>
public sealed class BedrockRuntimeClientFactory(IOptions<AwsOptions> options)
{
    public string ResolveProfile()
    {
        var envProfile = Environment.GetEnvironmentVariable("AWS_PROFILE");
        if (!string.IsNullOrWhiteSpace(envProfile))
        {
            return envProfile.Trim();
        }

        return options.Value.Profile?.Trim() ?? string.Empty;
    }

    public string ResolveRegion()
    {
        var envRegion = Environment.GetEnvironmentVariable("AWS_REGION")
                          ?? Environment.GetEnvironmentVariable("AWS_DEFAULT_REGION");
        if (!string.IsNullOrWhiteSpace(envRegion))
        {
            return envRegion.Trim();
        }

        var aws = options.Value;
        var regionName = !string.IsNullOrWhiteSpace(aws.Bedrock.Region) ? aws.Bedrock.Region : aws.Region;
        return string.IsNullOrWhiteSpace(regionName) ? "eu-north-1" : regionName;
    }

    public IAmazonBedrockRuntime Create()
    {
        var region = RegionEndpoint.GetBySystemName(ResolveRegion());
        var profile = ResolveProfile();

        if (!string.IsNullOrWhiteSpace(profile))
        {
            var chain = new CredentialProfileStoreChain();
            if (!chain.TryGetAWSCredentials(profile, out var credentials))
            {
                throw new InvalidOperationException(
                    $"AWS profile '{profile}' was not found. Run: aws configure --profile {profile}");
            }

            return new AmazonBedrockRuntimeClient(credentials, region);
        }

        return new AmazonBedrockRuntimeClient(region);
    }

    public bool TryValidateProfile(out string? error)
    {
        error = null;
        var profile = ResolveProfile();
        if (string.IsNullOrWhiteSpace(profile))
        {
            return true;
        }

        var chain = new CredentialProfileStoreChain();
        if (chain.TryGetAWSCredentials(profile, out _))
        {
            return true;
        }

        error = $"AWS profile '{profile}' was not found. Run: aws configure --profile {profile}";
        return false;
    }
}
