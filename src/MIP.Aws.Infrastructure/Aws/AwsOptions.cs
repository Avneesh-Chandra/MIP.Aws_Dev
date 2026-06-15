namespace MIP.Aws.Infrastructure.Aws;

public sealed class AwsOptions
{
    public const string SectionName = "Aws";
    public string Region { get; set; } = "us-east-1";

    /// <summary>AWS CLI profile name for local development (overridden by AWS_PROFILE env var).</summary>
    public string Profile { get; set; } = string.Empty;

    public AwsS3Options S3 { get; set; } = new();
    public AwsSesOptions Ses { get; set; } = new();
    public AwsBedrockOptions Bedrock { get; set; } = new();
    public AwsSecretsManagerOptions SecretsManager { get; set; } = new();
}

public sealed class AwsS3Options
{
    public bool Enabled { get; set; }
    public string BucketName { get; set; } = string.Empty;
    public string Prefix { get; set; } = "mip/";
}

public sealed class AwsSesOptions
{
    public bool Enabled { get; set; }
    public string SenderEmail { get; set; } = string.Empty;
    public string ConfigurationSet { get; set; } = string.Empty;
}

public sealed class AwsBedrockOptions
{
    public bool Enabled { get; set; }
    public string ModelId { get; set; } = "amazon.nova-lite-v1:0";
    public string Region { get; set; } = "eu-north-1";
    public int MaxTokens { get; set; } = 1200;
    public double Temperature { get; set; } = 0.2;
    public double TopP { get; set; } = 0.9;
    public int TimeoutSeconds { get; set; } = 60;
}

public sealed class AwsSecretsManagerOptions
{
    public bool Enabled { get; set; }
    public string Prefix { get; set; } = "mip/";
}
