namespace MIP.Aws.Infrastructure.Aws;

public sealed class AwsOptions
{
    public const string SectionName = "Aws";
    public string Region { get; set; } = "us-east-1";
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
    public string ModelId { get; set; } = string.Empty;
    public string Region { get; set; } = "us-east-1";
}

public sealed class AwsSecretsManagerOptions
{
    public bool Enabled { get; set; }
    public string Prefix { get; set; } = "mip/";
}
