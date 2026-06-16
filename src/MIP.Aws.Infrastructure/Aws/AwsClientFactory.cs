using Amazon;
using Amazon.Runtime;
using Amazon.Runtime.CredentialManagement;
using Amazon.SimpleEmailV2;

namespace MIP.Aws.Infrastructure.Aws;

/// <summary>Creates regional AWS SDK clients using optional CLI profile from config or AWS_PROFILE.</summary>
internal static class AwsClientFactory
{
    public static string ResolveProfile(AwsOptions options)
    {
        var envProfile = Environment.GetEnvironmentVariable("AWS_PROFILE");
        if (!string.IsNullOrWhiteSpace(envProfile))
        {
            return envProfile.Trim();
        }

        return options.Profile?.Trim() ?? string.Empty;
    }

    public static RegionEndpoint ResolveRegion(AwsOptions options) =>
        RegionEndpoint.GetBySystemName(string.IsNullOrWhiteSpace(options.Region) ? "eu-north-1" : options.Region);

    public static IAmazonSimpleEmailServiceV2 CreateSesClient(AwsOptions options)
    {
        var region = ResolveRegion(options);
        if (TryResolveCredentials(options, out var credentials))
        {
            return new AmazonSimpleEmailServiceV2Client(credentials, region);
        }

        return new AmazonSimpleEmailServiceV2Client(region);
    }

    private static bool TryResolveCredentials(AwsOptions options, out AWSCredentials credentials)
    {
        credentials = null!;
        var profile = ResolveProfile(options);
        if (string.IsNullOrWhiteSpace(profile))
        {
            return false;
        }

        var chain = new CredentialProfileStoreChain();
        if (!chain.TryGetAWSCredentials(profile, out var resolved) || resolved is null)
        {
            return false;
        }

        credentials = resolved;
        return true;
    }
}
