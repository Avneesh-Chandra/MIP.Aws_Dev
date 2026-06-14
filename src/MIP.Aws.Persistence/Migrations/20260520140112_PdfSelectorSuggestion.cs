using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MIP.Aws.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class PdfSelectorSuggestion : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "PdfDownloadExpectedAction",
                table: "NewsSources",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "PdfLinkExpectedAction",
                table: "NewsSources",
                type: "int",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "PdfSelectorSuggestions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    NewsSourceId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Url = table.Column<string>(type: "nvarchar(2048)", maxLength: 2048, nullable: false),
                    HtmlSnapshotPath = table.Column<string>(type: "nvarchar(2048)", maxLength: 2048, nullable: true),
                    ScreenshotPath = table.Column<string>(type: "nvarchar(2048)", maxLength: 2048, nullable: true),
                    SuggestedSelector = table.Column<string>(type: "nvarchar(512)", maxLength: 512, nullable: false),
                    SelectorType = table.Column<int>(type: "int", nullable: false),
                    Purpose = table.Column<int>(type: "int", nullable: false),
                    Confidence = table.Column<double>(type: "float", nullable: false),
                    Reason = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                    ExpectedAction = table.Column<int>(type: "int", nullable: false),
                    Status = table.Column<int>(type: "int", nullable: false),
                    ReviewedByUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    ReviewedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    TestFailureReason = table.Column<string>(type: "nvarchar(4000)", maxLength: 4000, nullable: true),
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
                    table.PrimaryKey("PK_PdfSelectorSuggestions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PdfSelectorSuggestions_NewsSources_NewsSourceId",
                        column: x => x.NewsSourceId,
                        principalTable: "NewsSources",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_PdfSelectorSuggestions_NewsSourceId_Status_CreatedAt",
                table: "PdfSelectorSuggestions",
                columns: new[] { "NewsSourceId", "Status", "CreatedAt" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "PdfSelectorSuggestions");

            migrationBuilder.DropColumn(
                name: "PdfDownloadExpectedAction",
                table: "NewsSources");

            migrationBuilder.DropColumn(
                name: "PdfLinkExpectedAction",
                table: "NewsSources");
        }
    }
}
