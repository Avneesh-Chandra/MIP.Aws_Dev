using Amazon.Runtime;

namespace MIP.Aws.Infrastructure.Aws;

public static class BedrockErrorClassifier
{
    public static string Classify(Exception ex)
    {
        if (ex is InvalidOperationException ioe && ioe.Message.Contains("profile", StringComparison.OrdinalIgnoreCase))
        {
            return ioe.Message;
        }

        if (ex is AmazonServiceException ase)
        {
            return ClassifyServiceException(ase);
        }

        if (ex.InnerException is AmazonServiceException inner)
        {
            return ClassifyServiceException(inner);
        }

        if (ex.Message.Contains("Unable to get IAM security credentials", StringComparison.OrdinalIgnoreCase)
            || ex.Message.Contains("No credentials", StringComparison.OrdinalIgnoreCase))
        {
            return "Missing AWS credentials. Configure a profile (aws configure --profile mip-dev) and set AWS_PROFILE=mip-dev.";
        }

        return ex.Message;
    }

    private static string ClassifyServiceException(AmazonServiceException ex) =>
        ex.ErrorCode switch
        {
            "UnrecognizedClientException" or "InvalidClientTokenId" =>
                "Missing or invalid AWS credentials. Run aws configure --profile mip-dev and set AWS_PROFILE=mip-dev.",
            "AccessDeniedException" =>
                "Access denied. Enable model access in AWS Console → Amazon Bedrock → Model access, and verify IAM permissions for bedrock:InvokeModel.",
            "ValidationException" =>
                $"Invalid model ID for region eu-north-1: {ex.Message}. Use amazon.nova-lite-v1:0 or eu.anthropic.claude-haiku-4-5-20251001-v1:0 (not Claude 3.5 Haiku, which is US-only).",
            "ThrottlingException" =>
                "Bedrock request was throttled. Wait and retry, or reduce concurrent AI recovery jobs.",
            "ResourceNotFoundException" =>
                "Model not available in this region. Verify Aws:Bedrock:ModelId and Aws:Bedrock:Region (eu-north-1).",
            _ when ex.StatusCode == System.Net.HttpStatusCode.Forbidden =>
                "Access denied. Enable Bedrock model access in the AWS Console for this account and region.",
            _ when ex.StatusCode == System.Net.HttpStatusCode.TooManyRequests =>
                "Bedrock request was throttled. Wait and retry.",
            _ => $"{ex.ErrorCode}: {ex.Message}"
        };
}
