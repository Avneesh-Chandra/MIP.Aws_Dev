using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MIP.Aws.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class PublicAcquisitionFields : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "AiSelectorSuggestionEnabled",
                table: "NewsSources",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "GenerateInternalReportAllowed",
                table: "NewsSources",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "LastInternalReportPath",
                table: "NewsSources",
                type: "nvarchar(2048)",
                maxLength: 2048,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "LastPdfDiscoveryOutcome",
                table: "NewsSources",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "LastPublicHtmlExtractedAt",
                table: "NewsSources",
                type: "datetimeoffset",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "PublicHtmlExtractionEnabled",
                table: "NewsSources",
                type: "bit",
                nullable: false,
                defaultValue: false);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "AiSelectorSuggestionEnabled",
                table: "NewsSources");

            migrationBuilder.DropColumn(
                name: "GenerateInternalReportAllowed",
                table: "NewsSources");

            migrationBuilder.DropColumn(
                name: "LastInternalReportPath",
                table: "NewsSources");

            migrationBuilder.DropColumn(
                name: "LastPdfDiscoveryOutcome",
                table: "NewsSources");

            migrationBuilder.DropColumn(
                name: "LastPublicHtmlExtractedAt",
                table: "NewsSources");

            migrationBuilder.DropColumn(
                name: "PublicHtmlExtractionEnabled",
                table: "NewsSources");
        }
    }
}
