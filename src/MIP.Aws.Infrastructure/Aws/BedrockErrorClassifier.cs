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
            "ValidationException" when ex.Message.Contains("inference profile", StringComparison.OrdinalIgnoreCase) =>
                "This region requires a Bedrock inference profile. Use eu.amazon.nova-lite-v1:0 in EU (not amazon.nova-lite-v1:0).",
            "ValidationException" =>
                $"Invalid Bedrock model ID: {ex.Message}. In EU use eu.amazon.nova-lite-v1:0 (Nova Lite inference profile).",
            "ThrottlingException" =>
                "Bedrock request was throttled. Wait and retry, or reduce concurrent AI recovery jobs.",
            "ResourceNotFoundException" =>
                "Model not available in this region. Verify Aws:Bedrock:ModelId (EU: eu.amazon.nova-lite-v1:0) and Aws:Bedrock:Region.",
            _ when ex.StatusCode == System.Net.HttpStatusCode.Forbidden =>
                "Access denied. Enable Bedrock model access in the AWS Console for this account and region.",
            _ when ex.StatusCode == System.Net.HttpStatusCode.TooManyRequests =>
                "Bedrock request was throttled. Wait and retry.",
            _ => $"{ex.ErrorCode}: {ex.Message}"
        };
}
