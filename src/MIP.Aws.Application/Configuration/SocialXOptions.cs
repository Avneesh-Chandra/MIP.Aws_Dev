namespace MIP.Aws.Application.Configuration;

public sealed class SocialXOptions
{
    public const string SectionName = "Social:X";

    public bool Enabled { get; set; }

    public bool MockMode { get; set; }

    public string? ClientId { get; set; }

    public string? ClientSecret { get; set; }

    public string? ApiKey { get; set; }

    public string? ApiSecret { get; set; }

    public string? BearerToken { get; set; }

    public string CallbackUrl { get; set; } = "https://localhost:5195/api/v1/social/accounts/x/callback";

    public string AuthorizeUrl { get; set; } = "https://twitter.com/i/oauth2/authorize";

    public string ApiBaseUrl { get; set; } = "https://api.x.com";

    public int MaxTweetLength { get; set; } = 280;
}
