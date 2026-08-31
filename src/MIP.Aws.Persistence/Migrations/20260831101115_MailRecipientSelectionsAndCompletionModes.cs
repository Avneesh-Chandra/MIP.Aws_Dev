using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MIP.Aws.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class MailRecipientSelectionsAndCompletionModes : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "PdfDownloadMailCompletionMode",
                table: "MailSettings",
                type: "nvarchar(32)",
                maxLength: 32,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "PdfDownloadMailSelectedRecipients",
                table: "MailSettings",
                type: "nvarchar(4000)",
                maxLength: 4000,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "StatusEmailCompletionMode",
                table: "MailSettings",
                type: "nvarchar(32)",
                maxLength: 32,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "StatusEmailSelectedRecipients",
                table: "MailSettings",
                type: "nvarchar(4000)",
                maxLength: 4000,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "PdfDownloadMailCompletionMode",
                table: "MailSettings");

            migrationBuilder.DropColumn(
                name: "PdfDownloadMailSelectedRecipients",
                table: "MailSettings");

            migrationBuilder.DropColumn(
                name: "StatusEmailCompletionMode",
                table: "MailSettings");

            migrationBuilder.DropColumn(
                name: "StatusEmailSelectedRecipients",
                table: "MailSettings");
        }
    }
}
