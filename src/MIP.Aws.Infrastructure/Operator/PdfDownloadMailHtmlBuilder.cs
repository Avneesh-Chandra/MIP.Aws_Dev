using System.Globalization;
using System.Net;
using MIP.Aws.Domain.Enums;

namespace MIP.Aws.Infrastructure.Operator;

public sealed record PdfDownloadMailItem(
    string SourceName,
    string FileName,
    DateOnly EditionDate,
    long SizeBytes,
    string ViewUrl,
    string DownloadUrl,
    bool IsAvailable = true);

public static class PdfDownloadMailHtmlBuilder
{
    // GFH brand palette (from theme assets).
    private const string RichBlack = "#332e2d";
    private const string Copper = "#af7846";
    private const string Umber = "#645d56";
    private const string Leather = "#c8c1b1";

    public static string Build(
        IReadOnlyList<PdfDownloadMailItem> items,
        string downloadAllUrl,
        string logoUrl,
        PdfDownloadMailDesign design = PdfDownloadMailDesign.Classic,
        string? heroImageUrl = null) =>
        design == PdfDownloadMailDesign.ModernGfh
            ? BuildModern(items, downloadAllUrl, logoUrl, heroImageUrl)
            : BuildClassic(items, downloadAllUrl);

    private static string BuildClassic(
        IReadOnlyList<PdfDownloadMailItem> items,
        string downloadAllUrl)
    {
        var rows = string.Join(Environment.NewLine, items.Select(BuildClassicRow));
        var (editionDate, editionSummary) = Summarize(items);

        return $$"""
            <!doctype html>
            <html lang="en">
            <body style="margin:0;padding:0;background:#eef2f6;color:#263746;font-family:Arial,Helvetica,sans-serif;">
              <table role="presentation" width="100%" cellpadding="0" cellspacing="0" border="0" style="background:#eef2f6;">
                <tr>
                  <td align="center" style="padding:28px 12px;">
                    <table role="presentation" width="680" cellpadding="0" cellspacing="0" border="0" style="width:100%;max-width:680px;background:#fff;border:1px solid #dce3ea;">
                      <tr>
                        <td style="padding:0;background:#102a43;">
                          <table role="presentation" width="100%" cellpadding="0" cellspacing="0" border="0">
                            <tr>
                              <td style="padding:22px 24px;">
                                <table role="presentation" cellpadding="0" cellspacing="0" border="0">
                                  <tr>
                                    <td valign="middle" style="padding-right:12px;">
                                      <div style="width:38px;height:38px;line-height:38px;text-align:center;background:#e31e4d;color:#fff;font-size:22px;font-weight:700;">◆</div>
                                    </td>
                                    <td valign="middle">
                                      <div style="color:#fff;font-size:25px;line-height:25px;font-weight:700;letter-spacing:1px;">GFH</div>
                                      <div style="color:#c7d7e5;font-size:10px;line-height:15px;font-weight:700;letter-spacing:2px;">MEDIA INTELLIGENCE</div>
                                    </td>
                                  </tr>
                                </table>
                              </td>
                              <td align="right" valign="middle" style="padding:22px 24px;">
                                {{DownloadAllButton(downloadAllUrl, "#e31e4d")}}
                              </td>
                            </tr>
                          </table>
                        </td>
                      </tr>
                      <tr>
                        <td style="padding:30px 32px 18px;">
                          <div style="color:#e31e4d;font-size:11px;font-weight:700;letter-spacing:1.4px;text-transform:uppercase;">Daily media briefing</div>
                          <div style="padding-top:7px;color:#102a43;font-size:25px;line-height:31px;font-weight:700;">Your newspaper editions are ready</div>
                          <div style="padding-top:8px;color:#657687;font-size:13px;line-height:20px;">{{Encode(editionSummary)}}
                            for {{Encode(editionDate)}}.</div>
                        </td>
                      </tr>
                      <tr>
                        <td style="padding:0 32px 14px;">
                          <table role="presentation" width="100%" cellpadding="0" cellspacing="0" border="0" style="background:#fff8fa;border:1px solid #f2d5dd;">
                            <tr>
                              <td style="padding:12px 14px;color:#6b4a54;font-size:11px;line-height:17px;">
                                <strong style="color:#e31e4d;">SECURE ACCESS</strong><br>
                                Available links open without a login and expire automatically after 24 hours.
                              </td>
                            </tr>
                          </table>
                        </td>
                      </tr>
                      <tr>
                        <td style="padding:0 32px 24px;">
            {{rows}}
                        </td>
                      </tr>
                      <tr>
                        <td style="padding:20px 32px;background:#102a43;border-top:4px solid #e31e4d;">
                          <div style="color:#fff;font-size:15px;font-weight:700;letter-spacing:.2px;">GFH Media Intelligence</div>
                          <div style="padding-top:5px;color:#c7d7e5;font-size:11px;line-height:17px;">Intelligence delivered, one edition at a time.</div>
                          <div style="padding-top:12px;color:#8fa8bb;font-size:10px;line-height:15px;">GFH Financial Group &nbsp;|&nbsp; Media Intelligence Desk<br>This automated message was generated after the daily edition download completed.</div>
                        </td>
                      </tr>
                    </table>
                  </td>
                </tr>
              </table>
            </body>
            </html>
            """;
    }

    private static string BuildModern(
        IReadOnlyList<PdfDownloadMailItem> items,
        string downloadAllUrl,
        string logoUrl,
        string? heroImageUrl)
    {
        var rows = string.Join(Environment.NewLine, items.Select(BuildModernRow));
        var (editionDate, editionSummary) = Summarize(items);
        var logoBlock = string.IsNullOrWhiteSpace(logoUrl)
            ? """
              <div style="color:#ffffff;font-size:22px;line-height:22px;font-weight:700;letter-spacing:1px;">GFH</div>
              <div style="color:#c8c1b1;font-size:10px;line-height:15px;font-weight:700;letter-spacing:1.5px;">MEDIA INTELLIGENCE</div>
              """
            : $"""
              <img src="{Encode(logoUrl)}" width="130" height="42" alt="GFH" style="display:block;border:0;outline:none;text-decoration:none;width:130px;height:auto;max-width:130px;">
              """;
        var heroBlock = string.IsNullOrWhiteSpace(heroImageUrl)
            ? $"""
              <tr>
                <td style="padding:28px 32px 12px;background:{RichBlack};font-family:Georgia,'Times New Roman',Times,serif;">
                  <div style="color:{Copper};font-size:28px;line-height:32px;font-weight:700;">The Daily</div>
                  <div style="color:{Copper};font-size:42px;line-height:46px;font-weight:700;">Newspaper</div>
                  <div style="padding-top:10px;color:{Leather};font-size:15px;line-height:22px;font-family:Arial,Helvetica,sans-serif;">Stay ahead of the story!</div>
                </td>
              </tr>
              """
            : $"""
              <tr>
                <td style="padding:0;background:{RichBlack};">
                  <img src="{Encode(heroImageUrl)}" width="680" alt="The Daily Newspaper — Stay ahead of the story!" style="display:block;border:0;outline:none;text-decoration:none;width:100%;max-width:680px;height:auto;">
                </td>
              </tr>
              """;

        return $$"""
            <!doctype html>
            <html lang="en" xmlns:v="urn:schemas-microsoft-com:vml" xmlns:o="urn:schemas-microsoft-com:office:office">
            <head>
              <!--[if gte mso 9]><xml><o:OfficeDocumentSettings><o:AllowPNG/><o:PixelsPerInch>96</o:PixelsPerInch></o:OfficeDocumentSettings></xml><![endif]-->
            </head>
            <body style="margin:0;padding:0;background:{{Leather}};color:{{RichBlack}};font-family:Arial,Helvetica,sans-serif;">
              <table role="presentation" width="100%" cellpadding="0" cellspacing="0" border="0" style="background:{{Leather}};">
                <tr>
                  <td align="center" style="padding:28px 12px;">
                    <table role="presentation" width="680" cellpadding="0" cellspacing="0" border="0" style="width:100%;max-width:680px;background:#f7f4ef;border:1px solid #b8b0a4;">
                      <tr>
                        <td style="padding:0;background:{{RichBlack}};">
                          <table role="presentation" width="100%" cellpadding="0" cellspacing="0" border="0">
                            <tr>
                              <td style="padding:18px 24px;" valign="middle">
                                {{logoBlock}}
                              </td>
                              <td align="right" valign="middle" style="padding:18px 24px;">
                                {{DownloadAllChip(downloadAllUrl, Copper)}}
                              </td>
                            </tr>
                          </table>
                        </td>
                      </tr>
            {{heroBlock}}
                      <tr>
                        <td style="padding:24px 32px 14px;font-family:Arial,Helvetica,sans-serif;">
                          <div style="color:{{RichBlack}};font-size:20px;line-height:26px;font-weight:700;">Today's editions are ready</div>
                          <div style="padding-top:8px;color:{{Umber}};font-size:13px;line-height:20px;">{{Encode(editionSummary)}}
                            for {{Encode(editionDate)}}.</div>
                        </td>
                      </tr>
                      <tr>
                        <td style="padding:0 32px 14px;font-family:Arial,Helvetica,sans-serif;">
                          <table role="presentation" width="100%" cellpadding="0" cellspacing="0" border="0" style="background:#efeae2;border:1px solid #d4cdc2;">
                            <tr>
                              <td style="padding:12px 14px;color:{{Umber}};font-size:11px;line-height:17px;">
                                <strong style="color:{{Copper}};">SECURE ACCESS</strong><br>
                                Available links open without a login and expire automatically after 24 hours.
                              </td>
                            </tr>
                          </table>
                        </td>
                      </tr>
                      <tr>
                        <td style="padding:0 32px 24px;font-family:Arial,Helvetica,sans-serif;">
            {{rows}}
                        </td>
                      </tr>
                      <tr>
                        <td style="padding:20px 32px;background:{{RichBlack}};border-top:4px solid {{Copper}};font-family:Arial,Helvetica,sans-serif;">
                          <div style="color:#fff;font-size:15px;font-weight:700;letter-spacing:.2px;">GFH Media Intelligence</div>
                          <div style="padding-top:5px;color:{{Leather}};font-size:11px;line-height:17px;">Stay ahead of the story.</div>
                          <div style="padding-top:12px;color:#9a9288;font-size:10px;line-height:15px;">GFH Financial Group &nbsp;|&nbsp; Media Intelligence Desk<br>This automated message was generated after the daily edition download completed.</div>
                        </td>
                      </tr>
                    </table>
                  </td>
                </tr>
              </table>
            </body>
            </html>
            """;
    }

    private static (string EditionDate, string EditionSummary) Summarize(IReadOnlyList<PdfDownloadMailItem> items)
    {
        var editionDate = items.Count == 0
            ? string.Empty
            : items[0].EditionDate.ToString("dd MMMM yyyy", CultureInfo.InvariantCulture);
        var availableCount = items.Count(item => item.IsAvailable);
        var unavailableCount = items.Count - availableCount;
        var editionSummary = unavailableCount == 0
            ? $"{availableCount} monitored editions were downloaded successfully"
            : $"{availableCount} monitored editions are available and {unavailableCount} are marked Not Available";
        return (editionDate, editionSummary);
    }

    private static string BuildClassicRow(PdfDownloadMailItem item) =>
        item.IsAvailable
            ? $$"""
                  <table role="presentation" width="100%" cellpadding="0" cellspacing="0" border="0" style="margin-top:10px;border:1px solid #dce3ea;background:#fff;">
                    <tr>
                      <td width="5" style="width:5px;background:#e31e4d;font-size:1px;line-height:1px;">&nbsp;</td>
                      <td style="padding:16px 14px;">
                        <div style="color:#e31e4d;font-size:10px;font-weight:700;letter-spacing:1px;text-transform:uppercase;">{{Encode(item.SourceName)}}</div>
                        <a href="{{Encode(item.ViewUrl)}}" style="display:block;padding-top:5px;color:#102a43;text-decoration:none;font-size:15px;line-height:20px;font-weight:700;">{{Encode(item.FileName)}}</a>
                        <div style="padding-top:6px;color:#718293;font-size:11px;line-height:16px;">Edition date: {{item.EditionDate.ToString("dd MMMM yyyy", CultureInfo.InvariantCulture)}} &nbsp;&bull;&nbsp; {{FormatSize(item.SizeBytes)}}</div>
                      </td>
                      <td align="right" valign="middle" style="padding:16px 14px;white-space:nowrap;">
                        <a href="{{Encode(item.ViewUrl)}}" style="display:inline-block;color:#102a43;text-decoration:none;border:1px solid #b9c8d5;border-radius:4px;padding:8px 10px;font-size:11px;font-weight:700;">OPEN</a>
                        <a href="{{Encode(item.DownloadUrl)}}" style="display:inline-block;margin-left:5px;background:#e31e4d;color:#fff;text-decoration:none;border-radius:4px;padding:9px 11px;font-size:11px;font-weight:700;">DOWNLOAD</a>
                      </td>
                    </tr>
                  </table>
            """
            : $$"""
                  <table role="presentation" width="100%" cellpadding="0" cellspacing="0" border="0" style="margin-top:10px;border:1px solid #dce3ea;background:#fff;">
                    <tr>
                      <td width="5" style="width:5px;background:#9aa8b5;font-size:1px;line-height:1px;">&nbsp;</td>
                      <td style="padding:16px 14px;">
                        <div style="color:#718293;font-size:10px;font-weight:700;letter-spacing:1px;text-transform:uppercase;">{{Encode(item.SourceName)}}</div>
                        <div style="padding-top:5px;color:#526273;font-size:15px;line-height:20px;font-weight:700;">Not Available</div>
                        <div style="padding-top:6px;color:#718293;font-size:11px;line-height:16px;">No PDF was stored for this source on the edition date.</div>
                      </td>
                    </tr>
                  </table>
            """;

    private static string BuildModernRow(PdfDownloadMailItem item) =>
        item.IsAvailable
            ? $$"""
                  <table role="presentation" width="100%" cellpadding="0" cellspacing="0" border="0" style="margin-top:10px;border:1px solid #d4cdc2;background:#fff;">
                    <tr>
                      <td width="5" style="width:5px;background:{{Copper}};font-size:1px;line-height:1px;">&nbsp;</td>
                      <td style="padding:16px 14px;">
                        <div style="color:{{Copper}};font-size:10px;font-weight:700;letter-spacing:1px;text-transform:uppercase;">{{Encode(item.SourceName)}}</div>
                        <a href="{{Encode(item.ViewUrl)}}" style="display:block;padding-top:5px;color:{{RichBlack}};text-decoration:none;font-size:15px;line-height:20px;font-weight:700;">{{Encode(item.FileName)}}</a>
                        <div style="padding-top:6px;color:{{Umber}};font-size:11px;line-height:16px;">Edition date: {{item.EditionDate.ToString("dd MMMM yyyy", CultureInfo.InvariantCulture)}} &nbsp;&bull;&nbsp; {{FormatSize(item.SizeBytes)}}</div>
                      </td>
                      <td align="right" valign="middle" style="padding:16px 14px;white-space:nowrap;">
                        <a href="{{Encode(item.ViewUrl)}}" style="display:inline-block;color:{{RichBlack}};text-decoration:none;border:1px solid #b8b0a4;border-radius:999px;padding:8px 12px;font-size:11px;font-weight:700;">OPEN</a>
                        <a href="{{Encode(item.DownloadUrl)}}" style="display:inline-block;margin-left:5px;background:{{Copper}};color:#fff;text-decoration:none;border-radius:999px;padding:9px 14px;font-size:11px;font-weight:700;">DOWNLOAD</a>
                      </td>
                    </tr>
                  </table>
            """
            : $$"""
                  <table role="presentation" width="100%" cellpadding="0" cellspacing="0" border="0" style="margin-top:10px;border:1px solid #d4cdc2;background:#fff;">
                    <tr>
                      <td width="5" style="width:5px;background:#9a9288;font-size:1px;line-height:1px;">&nbsp;</td>
                      <td style="padding:16px 14px;">
                        <div style="color:{{Umber}};font-size:10px;font-weight:700;letter-spacing:1px;text-transform:uppercase;">{{Encode(item.SourceName)}}</div>
                        <div style="padding-top:5px;color:{{RichBlack}};font-size:15px;line-height:20px;font-weight:700;">Not Available</div>
                        <div style="padding-top:6px;color:{{Umber}};font-size:11px;line-height:16px;">No PDF was stored for this source on the edition date.</div>
                      </td>
                    </tr>
                  </table>
            """;

    private static string DownloadAllButton(string? downloadAllUrl, string backgroundColor) =>
        string.IsNullOrWhiteSpace(downloadAllUrl)
            ? string.Empty
            : $"<a href=\"{Encode(downloadAllUrl)}\" style=\"display:inline-block;background:{backgroundColor};color:#fff;text-decoration:none;border-radius:4px;padding:12px 16px;font-size:12px;font-weight:700;letter-spacing:.3px;font-family:Arial,Helvetica,sans-serif;\">DOWNLOAD ALL &nbsp;&#x2192;</a>";

    /// <summary>Pill/chip CTA with VML roundrect for Outlook desktop (CSS radius is ignored there).</summary>
    private static string DownloadAllChip(string? downloadAllUrl, string backgroundColor)
    {
        if (string.IsNullOrWhiteSpace(downloadAllUrl))
        {
            return string.Empty;
        }

        var href = Encode(downloadAllUrl);
        return $"""
            <!--[if mso]>
            <v:roundrect xmlns:v="urn:schemas-microsoft-com:vml" xmlns:w="urn:schemas-microsoft-com:office:word" href="{href}" style="height:38px;v-text-anchor:middle;width:168px;" arcsize="50%" strokecolor="{backgroundColor}" fillcolor="{backgroundColor}">
              <w:anchorlock/>
              <center style="color:#ffffff;font-family:Arial,Helvetica,sans-serif;font-size:12px;font-weight:bold;">DOWNLOAD ALL &#8594;</center>
            </v:roundrect>
            <![endif]-->
            <!--[if !mso]><!-- -->
            <a href="{href}" style="display:inline-block;background:{backgroundColor};color:#ffffff;text-decoration:none;border-radius:999px;-webkit-border-radius:999px;padding:11px 22px;font-size:12px;font-weight:700;letter-spacing:.2px;font-family:Arial,Helvetica,sans-serif;line-height:16px;mso-hide:all;">DOWNLOAD ALL &nbsp;&#x2192;</a>
            <!--<![endif]-->
            """;
    }

    private static string Encode(string? value) => WebUtility.HtmlEncode(value ?? string.Empty);

    private static string FormatSize(long bytes)
    {
        if (bytes < 1024)
        {
            return $"{bytes} B";
        }

        var megabytes = bytes / (1024d * 1024d);
        return $"{megabytes:0.##} MB";
    }
}
