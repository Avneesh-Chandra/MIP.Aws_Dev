namespace MIP.Aws.Application.Features.SourceRecovery;

/// <summary>Removes AI suggestions that reference unsupported fields or empty patches.</summary>
public static class SourceRecoveryOptionSanitizer
{
    public static IReadOnlyList<SourceRecoveryOptionDto> Sanitize(IReadOnlyList<SourceRecoveryOptionDto> options)
    {
        if (options.Count == 0)
        {
            return options;
        }

        return options.Where(HasActionablePatch).ToList();
    }

    private static bool HasActionablePatch(SourceRecoveryOptionDto option)
    {
        var patch = option.Patch;
        return patch.UsernameSelector is not null
               || patch.PasswordSelector is not null
               || patch.SubmitSelector is not null
               || patch.DownloadSelector is not null
               || patch.LoginIconSelector is not null
               || patch.NewspaperCanvasSelector is not null
               || patch.ContextMenuSelector is not null
               || patch.DownloadMenuItemSelector is not null
               || patch.LoginSuccessSelector is not null
               || patch.SuccessUrlPattern is not null
               || patch.PdfDownloadSelector is not null
               || patch.PdfLinkSelector is not null
               || patch.BaseUrl is not null
               || patch.EditionUrl is not null
               || patch.PdfDiscoveryPageUrl is not null
               || patch.DownloadWaitTimeoutSeconds is not null
               || patch.UseHeadlessBrowser is not null;
    }
}
