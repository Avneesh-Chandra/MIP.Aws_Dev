using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MIP.Aws.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class NewsIngestionEnhancements : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_DownloadJobs_NewsSourceId_CreatedAt",
                table: "DownloadJobs");

            migrationBuilder.AddColumn<string>(
                name: "ProtectedCredentialPayload",
                table: "SourceCredentials",
                type: "nvarchar(4000)",
                maxLength: 4000,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Country",
                table: "NewsSources",
                type: "nvarchar(128)",
                maxLength: 128,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "DownloadFrequencyMinutes",
                table: "NewsSources",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "LastDownloadAt",
                table: "NewsSources",
                type: "datetimeoffset",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "RequiresAuthentication",
                table: "NewsSources",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "UseHeadlessBrowser",
                table: "NewsSources",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "CanonicalUrl",
                table: "ExtractedArticles",
                type: "nvarchar(2048)",
                maxLength: 2048,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ContentFingerprint",
                table: "ExtractedArticles",
                type: "nvarchar(128)",
                maxLength: 128,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "TagsJson",
                table: "ExtractedArticles",
                type: "nvarchar(4000)",
                maxLength: 4000,
                nullable: true);

            migrationBuilder.AddColumn<long>(
                name: "DurationMs",
                table: "DownloadJobs",
                type: "bigint",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "HttpStatusCode",
                table: "DownloadJobs",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "RetryCount",
                table: "DownloadJobs",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<bool>(
                name: "RobotsTxtAllowed",
                table: "DownloadJobs",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.CreateIndex(
                name: "IX_ExtractedArticles_ContentFingerprint",
                table: "ExtractedArticles",
                column: "ContentFingerprint",
                filter: "[ContentFingerprint] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_DownloadJobs_NewsSourceId_Status_CreatedAt",
                table: "DownloadJobs",
                columns: new[] { "NewsSourceId", "Status", "CreatedAt" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_ExtractedArticles_ContentFingerprint",
                table: "ExtractedArticles");

            migrationBuilder.DropIndex(
                name: "IX_DownloadJobs_NewsSourceId_Status_CreatedAt",
                table: "DownloadJobs");

            migrationBuilder.DropColumn(
                name: "ProtectedCredentialPayload",
                table: "SourceCredentials");

            migrationBuilder.DropColumn(
                name: "Country",
                table: "NewsSources");

            migrationBuilder.DropColumn(
                name: "DownloadFrequencyMinutes",
                table: "NewsSources");

            migrationBuilder.DropColumn(
                name: "LastDownloadAt",
                table: "NewsSources");

            migrationBuilder.DropColumn(
                name: "RequiresAuthentication",
                table: "NewsSources");

            migrationBuilder.DropColumn(
                name: "UseHeadlessBrowser",
                table: "NewsSources");

            migrationBuilder.DropColumn(
                name: "CanonicalUrl",
                table: "ExtractedArticles");

            migrationBuilder.DropColumn(
                name: "ContentFingerprint",
                table: "ExtractedArticles");

            migrationBuilder.DropColumn(
                name: "TagsJson",
                table: "ExtractedArticles");

            migrationBuilder.DropColumn(
                name: "DurationMs",
                table: "DownloadJobs");

            migrationBuilder.DropColumn(
                name: "HttpStatusCode",
                table: "DownloadJobs");

            migrationBuilder.DropColumn(
                name: "RetryCount",
                table: "DownloadJobs");

            migrationBuilder.DropColumn(
                name: "RobotsTxtAllowed",
                table: "DownloadJobs");

            migrationBuilder.CreateIndex(
                name: "IX_DownloadJobs_NewsSourceId_CreatedAt",
                table: "DownloadJobs",
                columns: new[] { "NewsSourceId", "CreatedAt" });
        }
    }
}
