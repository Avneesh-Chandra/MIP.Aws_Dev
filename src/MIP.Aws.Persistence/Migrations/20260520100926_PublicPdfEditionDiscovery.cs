using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MIP.Aws.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class PublicPdfEditionDiscovery : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "LastPdfDiscoveredAt",
                table: "NewsSources",
                type: "datetimeoffset",
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "LastPdfDownloadedAt",
                table: "NewsSources",
                type: "datetimeoffset",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "LastPdfUrl",
                table: "NewsSources",
                type: "nvarchar(2048)",
                maxLength: 2048,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "LastSavedPdfPath",
                table: "NewsSources",
                type: "nvarchar(2048)",
                maxLength: 2048,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "MinimumPdfSizeKb",
                table: "NewsSources",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<string>(
                name: "PdfDatePattern",
                table: "NewsSources",
                type: "nvarchar(256)",
                maxLength: 256,
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "PdfDiscoveryEnabled",
                table: "NewsSources",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<int>(
                name: "PdfDiscoveryMode",
                table: "NewsSources",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<string>(
                name: "PdfDiscoveryPageUrl",
                table: "NewsSources",
                type: "nvarchar(2048)",
                maxLength: 2048,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "PdfDownloadSelector",
                table: "NewsSources",
                type: "nvarchar(512)",
                maxLength: 512,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "PdfLinkKeywords",
                table: "NewsSources",
                type: "nvarchar(2000)",
                maxLength: 2000,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "PdfLinkSelector",
                table: "NewsSources",
                type: "nvarchar(512)",
                maxLength: 512,
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "PreferLatestEdition",
                table: "NewsSources",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "PreferTodayEdition",
                table: "NewsSources",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "RequirePdfContentType",
                table: "NewsSources",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.CreateTable(
                name: "PdfEditionDownloads",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    NewsSourceId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    DownloadJobId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    DownloadedFileId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    SourceUrl = table.Column<string>(type: "nvarchar(2048)", maxLength: 2048, nullable: false),
                    SavedPath = table.Column<string>(type: "nvarchar(2048)", maxLength: 2048, nullable: true),
                    FileName = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: false),
                    FileSizeBytes = table.Column<long>(type: "bigint", nullable: true),
                    Sha256Hash = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: true),
                    ContentType = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: false),
                    EditionDate = table.Column<DateOnly>(type: "date", nullable: false),
                    DiscoveryConfidence = table.Column<double>(type: "float", nullable: false),
                    DiscoveryMethod = table.Column<int>(type: "int", nullable: false),
                    Status = table.Column<int>(type: "int", nullable: false),
                    FailureReason = table.Column<string>(type: "nvarchar(4000)", maxLength: 4000, nullable: true),
                    DiscoveredAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    DownloadedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    CreatedBy = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    ModifiedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    ModifiedBy = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PdfEditionDownloads", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PdfEditionDownloads_DownloadJobs_DownloadJobId",
                        column: x => x.DownloadJobId,
                        principalTable: "DownloadJobs",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.NoAction);
                    table.ForeignKey(
                        name: "FK_PdfEditionDownloads_DownloadedFiles_DownloadedFileId",
                        column: x => x.DownloadedFileId,
                        principalTable: "DownloadedFiles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.NoAction);
                    table.ForeignKey(
                        name: "FK_PdfEditionDownloads_NewsSources_NewsSourceId",
                        column: x => x.NewsSourceId,
                        principalTable: "NewsSources",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_PdfEditionDownloads_DownloadedFileId",
                table: "PdfEditionDownloads",
                column: "DownloadedFileId");

            migrationBuilder.CreateIndex(
                name: "IX_PdfEditionDownloads_DownloadJobId",
                table: "PdfEditionDownloads",
                column: "DownloadJobId");

            migrationBuilder.CreateIndex(
                name: "IX_PdfEditionDownloads_NewsSourceId_EditionDate_Status",
                table: "PdfEditionDownloads",
                columns: new[] { "NewsSourceId", "EditionDate", "Status" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "PdfEditionDownloads");

            migrationBuilder.DropColumn(
                name: "LastPdfDiscoveredAt",
                table: "NewsSources");

            migrationBuilder.DropColumn(
                name: "LastPdfDownloadedAt",
                table: "NewsSources");

            migrationBuilder.DropColumn(
                name: "LastPdfUrl",
                table: "NewsSources");

            migrationBuilder.DropColumn(
                name: "LastSavedPdfPath",
                table: "NewsSources");

            migrationBuilder.DropColumn(
                name: "MinimumPdfSizeKb",
                table: "NewsSources");

            migrationBuilder.DropColumn(
                name: "PdfDatePattern",
                table: "NewsSources");

            migrationBuilder.DropColumn(
                name: "PdfDiscoveryEnabled",
                table: "NewsSources");

            migrationBuilder.DropColumn(
                name: "PdfDiscoveryMode",
                table: "NewsSources");

            migrationBuilder.DropColumn(
                name: "PdfDiscoveryPageUrl",
                table: "NewsSources");

            migrationBuilder.DropColumn(
                name: "PdfDownloadSelector",
                table: "NewsSources");

            migrationBuilder.DropColumn(
                name: "PdfLinkKeywords",
                table: "NewsSources");

            migrationBuilder.DropColumn(
                name: "PdfLinkSelector",
                table: "NewsSources");

            migrationBuilder.DropColumn(
                name: "PreferLatestEdition",
                table: "NewsSources");

            migrationBuilder.DropColumn(
                name: "PreferTodayEdition",
                table: "NewsSources");

            migrationBuilder.DropColumn(
                name: "RequirePdfContentType",
                table: "NewsSources");
        }
    }
}
