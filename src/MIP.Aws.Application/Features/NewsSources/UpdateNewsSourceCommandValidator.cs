using FluentValidation;
using MIP.Aws.Domain.Enums;

namespace MIP.Aws.Application.Features.NewsSources;

public sealed class UpdateNewsSourceCommandValidator : AbstractValidator<UpdateNewsSourceCommand>
{
    public UpdateNewsSourceCommandValidator()
    {
        RuleFor(x => x.Id).NotEmpty();
        RuleFor(x => x.Name).NotEmpty().MaximumLength(256);
        RuleFor(x => x.BaseUrl).NotEmpty().MaximumLength(2048).Must(BeHttp).WithMessage("BaseUrl must be an absolute http(s) URL.");
        RuleFor(x => x.DefaultLanguage).MaximumLength(16);
        RuleFor(x => x.Country).MaximumLength(128);
        RuleFor(x => x.ConnectorKey).MaximumLength(128);
        RuleFor(x => x.CronExpression).MaximumLength(128);
        RuleFor(x => x.ScheduleTimeZoneId).MaximumLength(128);
        RuleFor(x => x.CredentialUsername).MaximumLength(256);
        RuleFor(x => x.CredentialPassword).MaximumLength(512);
        RuleFor(x => x.PortalUsername).MaximumLength(256);
        RuleFor(x => x.LoginUrl).MaximumLength(2048);
        RuleFor(x => x.EditionUrl).MaximumLength(2048);
        RuleFor(x => x.LogoutUrl).MaximumLength(2048);
        RuleFor(x => x.UsernameSelector).MaximumLength(512);
        RuleFor(x => x.PasswordSelector).MaximumLength(512);
        RuleFor(x => x.SubmitSelector).MaximumLength(512);
        RuleFor(x => x.DownloadSelector).MaximumLength(512);
        RuleFor(x => x.LoginSuccessSelector).MaximumLength(512);
        RuleFor(x => x.SuccessUrlPattern).MaximumLength(512);
        RuleFor(x => x.OtpInstructions).MaximumLength(2000);
        RuleFor(x => x.Notes).MaximumLength(2000);
        RuleFor(x => x.DownloadFrequencyMinutes).InclusiveBetween(5, 10080).When(x => x.DownloadFrequencyMinutes.HasValue);
        RuleFor(x => x.AssistedSessionTimeoutMinutes).InclusiveBetween(1, 240).When(x => x.AssistedSessionTimeoutMinutes.HasValue);

        RuleFor(x => x.AcquisitionMode)
            .Must(m => m is ContentAcquisitionMode.LicensedWebPortalSubscriber
                or ContentAcquisitionMode.LicensedFeedOrApi
                or ContentAcquisitionMode.PartnerManagedConnector)
            .When(x => x.SourceType == NewsSourceType.WebPortalLogin)
            .WithMessage("WebPortalLogin sources must use a licensed acquisition mode (e.g. LicensedWebPortalSubscriber).");

        RuleFor(x => x.LoginUrl).NotEmpty().Must(BeHttp).When(x => x.SourceType == NewsSourceType.WebPortalLogin);
        RuleFor(x => x.EditionUrl).NotEmpty().Must(BeHttp).When(x => x.SourceType == NewsSourceType.WebPortalLogin);
        // DownloadSelector is optional (e.g. PressReader viewer has no file download); automation captures HTML when empty.

        // ───── Manual-assisted MFA / OTP compliance guard ─────
        RuleFor(x => x.CredentialPassword).Empty()
            .When(IsManualAssisted)
            .WithMessage("Manual-assisted (MFA/OTP) sources must NOT carry a stored password. The operator authenticates interactively each session.");
        RuleFor(x => x.ManualLoginRequired).Equal(true)
            .When(x => x.RequiresMfa || x.RequiresOtp || x.LoginMethod is PortalLoginMethod.ManualOtpAssisted or PortalLoginMethod.ManualBrowserSession)
            .WithMessage("ManualLoginRequired must be enabled for MFA / OTP / manual-assisted login methods.");
        RuleFor(x => x.LoginMethod)
            .Must(m => m is PortalLoginMethod.ManualOtpAssisted or PortalLoginMethod.ManualBrowserSession)
            .When(x => x.RequiresOtp || x.RequiresMfa || x.ManualLoginRequired)
            .WithMessage("LoginMethod must be ManualOtpAssisted or ManualBrowserSession for MFA / OTP / manual-assisted sources.");
        RuleFor(x => x.AssistedSessionTimeoutMinutes)
            .NotNull().InclusiveBetween(1, 240)
            .When(IsManualAssisted)
            .WithMessage("Manual-assisted sources must declare AssistedSessionTimeoutMinutes (1..240).");
    }

    private static bool IsManualAssisted(UpdateNewsSourceCommand c) =>
        c.ManualLoginRequired
            || c.RequiresMfa
            || c.RequiresOtp
            || c.LoginMethod is PortalLoginMethod.ManualOtpAssisted or PortalLoginMethod.ManualBrowserSession;

    private static bool BeHttp(string? url) =>
        !string.IsNullOrWhiteSpace(url) &&
        Uri.TryCreate(url, UriKind.Absolute, out var u) &&
        (u.Scheme == Uri.UriSchemeHttp || u.Scheme == Uri.UriSchemeHttps);
}
