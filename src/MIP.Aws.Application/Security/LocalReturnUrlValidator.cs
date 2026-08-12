namespace MIP.Aws.Application.Security;

public static class LocalReturnUrlValidator
{
    public static bool IsValid(string? returnUrl)
    {
        if (string.IsNullOrWhiteSpace(returnUrl))
        {
            return false;
        }

        var url = returnUrl.Trim();
        if (!url.StartsWith('/'))
        {
            return false;
        }

        if (url.StartsWith("//", StringComparison.Ordinal))
        {
            return false;
        }

        if (url.Contains("://", StringComparison.Ordinal))
        {
            return false;
        }

        return true;
    }
}
