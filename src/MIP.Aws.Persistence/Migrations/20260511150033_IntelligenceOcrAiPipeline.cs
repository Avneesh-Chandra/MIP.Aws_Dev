using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MIP.Aws.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class IntelligenceOcrAiPipeline : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "AiProcessingAttempts",
                table: "ExtractedArticles",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "EndPage",
                table: "ExtractedArticles",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ExecutiveBrief",
                table: "ExtractedArticles",
                type: "nvarchar(4000)",
                maxLength: 4000,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "GfhContextExplanation",
                table: "ExtractedArticles",
                type: "nvarchar(4000)",
                maxLength: 4000,
                nullable: true);

            migrationBuilder.AddColumn<double>(
                name: "GfhRelevanceScore",
                table: "ExtractedArticles",
                type: "float",
                nullable: false,
                defaultValue: 0.0);

            migrationBuilder.AddColumn<int>(
                name: "GfhRelevanceTier",
                table: "ExtractedArticles",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<string>(
                name: "GfhSignalsJson",
                table: "ExtractedArticles",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "IntelligenceStatus",
                table: "ExtractedArticles",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "MarketImpactEstimate",
                table: "ExtractedArticles",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "StartPage",
                table: "ExtractedArticles",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ExecutiveNarrative",
                table: "AISummaries",
                type: "nvarchar(max)",
                maxLength: 8000,
                nullable: true);

            migrationBuilder.AddColumn<double>(
                name: "SentimentConfidence",
                table: "AISummaries",
                type: "float",
                nullable: false,
                defaultValue: 0.0);

            migrationBuilder.AddColumn<string>(
                name: "SentimentRationale",
                table: "AISummaries",
                type: "nvarchar(4000)",
                maxLength: 4000,
                nullable: true);

            migrationBuilder.CreateTable(
                name: "ArticleClassifications",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ExtractedArticleId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Category = table.Column<int>(type: "int", nullable: false),
                    Confidence = table.Column<double>(type: "float", nullable: false),
                    IsPrimary = table.Column<bool>(type: "bit", nullable: false),
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
                    table.PrimaryKey("PK_ArticleClassifications", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ArticleClassifications_ExtractedArticles_ExtractedArticleId",
                        column: x => x.ExtractedArticleId,
                        principalTable: "ExtractedArticles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "ArticleKeywords",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ExtractedArticleId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Keyword = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: false),
                    Language = table.Column<string>(type: "nvarchar(16)", maxLength: 16, nullable: false),
                    Weight = table.Column<double>(type: "float", nullable: false),
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
                    table.PrimaryKey("PK_ArticleKeywords", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ArticleKeywords_ExtractedArticles_ExtractedArticleId",
                        column: x => x.ExtractedArticleId,
                        principalTable: "ExtractedArticles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "ArticlePages",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ExtractedArticleId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    PageNumber = table.Column<int>(type: "int", nullable: false),
                    Confidence = table.Column<double>(type: "float", nullable: true),
                    Snippet = table.Column<string>(type: "nvarchar(4000)", maxLength: 4000, nullable: true),
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
                    table.PrimaryKey("PK_ArticlePages", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ArticlePages_ExtractedArticles_ExtractedArticleId",
                        column: x => x.ExtractedArticleId,
                        principalTable: "ExtractedArticles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "ArticleSentiments",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ExtractedArticleId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Polarity = table.Column<int>(type: "int", nullable: false),
                    Confidence = table.Column<double>(type: "float", nullable: false),
                    Explanation = table.Column<string>(type: "nvarchar(4000)", maxLength: 4000, nullable: true),
                    MarketImpact = table.Column<int>(type: "int", nullable: false),
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
                    table.PrimaryKey("PK_ArticleSentiments", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ArticleSentiments_ExtractedArticles_ExtractedArticleId",
                        column: x => x.ExtractedArticleId,
                        principalTable: "ExtractedArticles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "OcrProcessingJobs",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    DownloadedFileId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Status = table.Column<int>(type: "int", nullable: false),
                    StartedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    CompletedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    ErrorMessage = table.Column<string>(type: "nvarchar(4000)", maxLength: 4000, nullable: true),
                    RetryCount = table.Column<int>(type: "int", nullable: false),
                    ResultManifestRelativePath = table.Column<string>(type: "nvarchar(2048)", maxLength: 2048, nullable: true),
                    AveragePageConfidence = table.Column<double>(type: "float", nullable: true),
                    CorrelationId = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: true),
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
                    table.PrimaryKey("PK_OcrProcessingJobs", x => x.Id);
                    table.ForeignKey(
                        name: "FK_OcrProcessingJobs_DownloadedFiles_DownloadedFileId",
                        column: x => x.DownloadedFileId,
                        principalTable: "DownloadedFiles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "OcrPageResults",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    OcrProcessingJobId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    PageNumber = table.Column<int>(type: "int", nullable: false),
                    PageText = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    ConfidenceScore = table.Column<double>(type: "float", nullable: true),
                    PageJsonRelativePath = table.Column<string>(type: "nvarchar(2048)", maxLength: 2048, nullable: true),
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
                    table.PrimaryKey("PK_OcrPageResults", x => x.Id);
                    table.ForeignKey(
                        name: "FK_OcrPageResults_OcrProcessingJobs_OcrProcessingJobId",
                        column: x => x.OcrProcessingJobId,
                        principalTable: "OcrProcessingJobs",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ExtractedArticles_IntelligenceStatus_GfhRelevanceTier",
                table: "ExtractedArticles",
                columns: new[] { "IntelligenceStatus", "GfhRelevanceTier" });

            migrationBuilder.CreateIndex(
                name: "IX_ArticleClassifications_ExtractedArticleId_Category",
                table: "ArticleClassifications",
                columns: new[] { "ExtractedArticleId", "Category" });

            migrationBuilder.CreateIndex(
                name: "IX_ArticleKeywords_ExtractedArticleId_Keyword",
                table: "ArticleKeywords",
                columns: new[] { "ExtractedArticleId", "Keyword" });

            migrationBuilder.CreateIndex(
                name: "IX_ArticlePages_ExtractedArticleId_PageNumber",
                table: "ArticlePages",
                columns: new[] { "ExtractedArticleId", "PageNumber" });

            migrationBuilder.CreateIndex(
                name: "IX_ArticleSentiments_ExtractedArticleId",
                table: "ArticleSentiments",
                column: "ExtractedArticleId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_OcrPageResults_OcrProcessingJobId_PageNumber",
                table: "OcrPageResults",
                columns: new[] { "OcrProcessingJobId", "PageNumber" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_OcrProcessingJobs_DownloadedFileId_Status_CreatedAt",
                table: "OcrProcessingJobs",
                columns: new[] { "DownloadedFileId", "Status", "CreatedAt" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ArticleClassifications");

            migrationBuilder.DropTable(
                name: "ArticleKeywords");

            migrationBuilder.DropTable(
                name: "ArticlePages");

            migrationBuilder.DropTable(
                name: "ArticleSentiments");

            migrationBuilder.DropTable(
                name: "OcrPageResults");

            migrationBuilder.DropTable(
                name: "OcrProcessingJobs");

            migrationBuilder.DropIndex(
                name: "IX_ExtractedArticles_IntelligenceStatus_GfhRelevanceTier",
                table: "ExtractedArticles");

            migrationBuilder.DropColumn(
                name: "AiProcessingAttempts",
                table: "ExtractedArticles");

            migrationBuilder.DropColumn(
                name: "EndPage",
                table: "ExtractedArticles");

            migrationBuilder.DropColumn(
                name: "ExecutiveBrief",
                table: "ExtractedArticles");

            migrationBuilder.DropColumn(
                name: "GfhContextExplanation",
                table: "ExtractedArticles");

            migrationBuilder.DropColumn(
                name: "GfhRelevanceScore",
                table: "ExtractedArticles");

            migrationBuilder.DropColumn(
                name: "GfhRelevanceTier",
                table: "ExtractedArticles");

            migrationBuilder.DropColumn(
                name: "GfhSignalsJson",
                table: "ExtractedArticles");

            migrationBuilder.DropColumn(
                name: "IntelligenceStatus",
                table: "ExtractedArticles");

            migrationBuilder.DropColumn(
                name: "MarketImpactEstimate",
                table: "ExtractedArticles");

            migrationBuilder.DropColumn(
                name: "StartPage",
                table: "ExtractedArticles");

            migrationBuilder.DropColumn(
                name: "ExecutiveNarrative",
                table: "AISummaries");

            migrationBuilder.DropColumn(
                name: "SentimentConfidence",
                table: "AISummaries");

            migrationBuilder.DropColumn(
                name: "SentimentRationale",
                table: "AISummaries");
        }
    }
}
