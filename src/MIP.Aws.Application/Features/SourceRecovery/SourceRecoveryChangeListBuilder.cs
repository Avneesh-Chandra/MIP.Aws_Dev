namespace MIP.Aws.Application.Features.SourceRecovery;

public static class SourceRecoveryChangeListBuilder
{
    public static IReadOnlyList<(string Field, string? BeforeValue, string? AfterValue)> Build(
        SourceRecoveryConfigurationPatchDto before,
        SourceRecoveryConfigurationPatchDto after)
    {
        var list = new List<(string, string?, string?)>();
        void Add(string name, string? cur, string? next)
        {
            if (next is not null && !string.Equals(cur, next, StringComparison.Ordinal))
            {
                list.Add((name, cur, next));
            }
        }

        Add("UsernameSelector", before.UsernameSelector, after.UsernameSelector);
        Add("PasswordSelector", before.PasswordSelector, after.PasswordSelector);
        Add("SubmitSelector", before.SubmitSelector, after.SubmitSelector);
        Add("DownloadSelector", before.DownloadSelector, after.DownloadSelector);
        Add("LoginIconSelector", before.LoginIconSelector, after.LoginIconSelector);
        Add("NewspaperCanvasSelector", before.NewspaperCanvasSelector, after.NewspaperCanvasSelector);
        Add("ContextMenuSelector", before.ContextMenuSelector, after.ContextMenuSelector);
        Add("DownloadMenuItemSelector", before.DownloadMenuItemSelector, after.DownloadMenuItemSelector);
        Add("LoginSuccessSelector", before.LoginSuccessSelector, after.LoginSuccessSelector);
        Add("SuccessUrlPattern", before.SuccessUrlPattern, after.SuccessUrlPattern);
        Add("PdfDownloadSelector", before.PdfDownloadSelector, after.PdfDownloadSelector);
        Add("PdfLinkSelector", before.PdfLinkSelector, after.PdfLinkSelector);
        Add("BaseUrl", before.BaseUrl, after.BaseUrl);
        Add("EditionUrl", before.EditionUrl, after.EditionUrl);
        Add("PdfDiscoveryPageUrl", before.PdfDiscoveryPageUrl, after.PdfDiscoveryPageUrl);
        if (after.DownloadWaitTimeoutSeconds is int wait
            && wait != before.DownloadWaitTimeoutSeconds)
        {
            list.Add(("DownloadWaitTimeoutSeconds", before.DownloadWaitTimeoutSeconds.ToString(), wait.ToString()));
        }

        AddBool("UseHeadlessBrowser", before.UseHeadlessBrowser, after.UseHeadlessBrowser);

        return list;

        void AddBool(string field, bool? beforeValue, bool? afterValue)
        {
            if (afterValue is bool value && beforeValue != value)
            {
                list.Add((field, beforeValue?.ToString() ?? "(empty)", value.ToString()));
            }
        }
    }
}
