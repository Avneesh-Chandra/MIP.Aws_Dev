using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MIP.Aws.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class PdfDownloadMail : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "PdfDownloadMailEnabled",
                table: "MailSettings",
                type: "bit",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "PdfDownloadMailRecipient",
                table: "MailSettings",
                type: "nvarchar(320)",
                maxLength: 320,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "PdfDownloadMailSubject",
                table: "MailSettings",
                type: "nvarchar(256)",
                maxLength: 256,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "PdfDownloadMailTimeUtc",
                table: "MailSettings",
                type: "nvarchar(5)",
                maxLength: 5,
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "PdfDownloadMailSentAt",
                table: "DownloadMonitorBatchRuns",
                type: "datetimeoffset",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "PdfDownloadMailEnabled",
                table: "MailSettings");

            migrationBuilder.DropColumn(
                name: "PdfDownloadMailRecipient",
                table: "MailSettings");

            migrationBuilder.DropColumn(
                name: "PdfDownloadMailSubject",
                table: "MailSettings");

            migrationBuilder.DropColumn(
                name: "PdfDownloadMailTimeUtc",
                table: "MailSettings");

            migrationBuilder.DropColumn(
                name: "PdfDownloadMailSentAt",
                table: "DownloadMonitorBatchRuns");
        }
    }
}
