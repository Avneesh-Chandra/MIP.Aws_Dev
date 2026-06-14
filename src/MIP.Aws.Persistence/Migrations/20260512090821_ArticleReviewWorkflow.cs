using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MIP.Aws.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class ArticleReviewWorkflow : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "AnalystHeadline",
                table: "ExtractedArticles",
                type: "nvarchar(1024)",
                maxLength: 1024,
                nullable: true);

            migrationBuilder.AddColumn<double>(
                name: "AnalystRelevanceScore",
                table: "ExtractedArticles",
                type: "float",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "AnalystSentiment",
                table: "ExtractedArticles",
                type: "nvarchar(32)",
                maxLength: 32,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "AnalystSummary",
                table: "ExtractedArticles",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "AnalystTagsJson",
                table: "ExtractedArticles",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "AssignedReviewerId",
                table: "ExtractedArticles",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "LastReviewActionAt",
                table: "ExtractedArticles",
                type: "datetimeoffset",
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "ReviewSlaDueAt",
                table: "ExtractedArticles",
                type: "datetimeoffset",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "ReviewState",
                table: "ExtractedArticles",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.CreateTable(
                name: "ArticleAnnotations",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ExtractedArticleId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    AuthorUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    AuthorEmail = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: false),
                    Kind = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false),
                    Label = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: true),
                    Note = table.Column<string>(type: "nvarchar(4000)", maxLength: 4000, nullable: true),
                    StartOffset = table.Column<int>(type: "int", nullable: true),
                    EndOffset = table.Column<int>(type: "int", nullable: true),
                    AnchorText = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    IsPinned = table.Column<bool>(type: "bit", nullable: false),
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
                    table.PrimaryKey("PK_ArticleAnnotations", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ArticleAnnotations_ExtractedArticles_ExtractedArticleId",
                        column: x => x.ExtractedArticleId,
                        principalTable: "ExtractedArticles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "ArticleReviewActions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ExtractedArticleId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ActorUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ActorEmail = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: false),
                    FromState = table.Column<int>(type: "int", nullable: false),
                    ToState = table.Column<int>(type: "int", nullable: false),
                    Action = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    Reason = table.Column<string>(type: "nvarchar(4000)", maxLength: 4000, nullable: true),
                    OverridesJson = table.Column<string>(type: "nvarchar(max)", nullable: true),
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
                    table.PrimaryKey("PK_ArticleReviewActions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ArticleReviewActions_ExtractedArticles_ExtractedArticleId",
                        column: x => x.ExtractedArticleId,
                        principalTable: "ExtractedArticles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "ArticleReviewComments",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ExtractedArticleId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    AuthorUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    AuthorEmail = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: false),
                    Body = table.Column<string>(type: "nvarchar(4000)", maxLength: 4000, nullable: false),
                    MentionedUserIds = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                    ParentCommentId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    IsPinned = table.Column<bool>(type: "bit", nullable: false),
                    IsResolved = table.Column<bool>(type: "bit", nullable: false),
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
                    table.PrimaryKey("PK_ArticleReviewComments", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ArticleReviewComments_ArticleReviewComments_ParentCommentId",
                        column: x => x.ParentCommentId,
                        principalTable: "ArticleReviewComments",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_ArticleReviewComments_ExtractedArticles_ExtractedArticleId",
                        column: x => x.ExtractedArticleId,
                        principalTable: "ExtractedArticles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "ExecutiveBriefs",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Title = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: false),
                    Subtitle = table.Column<string>(type: "nvarchar(512)", maxLength: 512, nullable: true),
                    IntroNarrative = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    ClosingNotes = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Status = table.Column<int>(type: "int", nullable: false),
                    PublishedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    PublishedByUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    PublishedByEmail = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    RenderedPdfRelativePath = table.Column<string>(type: "nvarchar(2048)", maxLength: 2048, nullable: true),
                    RenderedHtmlRelativePath = table.Column<string>(type: "nvarchar(2048)", maxLength: 2048, nullable: true),
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
                    table.PrimaryKey("PK_ExecutiveBriefs", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "ReviewAssignments",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ExtractedArticleId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    AssigneeUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    AssigneeEmail = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: false),
                    AssignedByUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    AssignedByEmail = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: false),
                    AssignedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    DueAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    CompletedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    Status = table.Column<int>(type: "int", nullable: false),
                    Notes = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
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
                    table.PrimaryKey("PK_ReviewAssignments", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ReviewAssignments_ExtractedArticles_ExtractedArticleId",
                        column: x => x.ExtractedArticleId,
                        principalTable: "ExtractedArticles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "ExecutiveBriefItems",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ExecutiveBriefId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ExtractedArticleId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    DisplayOrder = table.Column<int>(type: "int", nullable: false),
                    Commentary = table.Column<string>(type: "nvarchar(max)", nullable: true),
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
                    table.PrimaryKey("PK_ExecutiveBriefItems", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ExecutiveBriefItems_ExecutiveBriefs_ExecutiveBriefId",
                        column: x => x.ExecutiveBriefId,
                        principalTable: "ExecutiveBriefs",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_ExecutiveBriefItems_ExtractedArticles_ExtractedArticleId",
                        column: x => x.ExtractedArticleId,
                        principalTable: "ExtractedArticles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "ExecutiveQueueItems",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ExtractedArticleId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Priority = table.Column<int>(type: "int", nullable: false),
                    ImpactLevel = table.Column<int>(type: "int", nullable: false),
                    ExecutiveNote = table.Column<string>(type: "nvarchar(4000)", maxLength: 4000, nullable: true),
                    Recommendation = table.Column<string>(type: "nvarchar(4000)", maxLength: 4000, nullable: true),
                    EscalatedByUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    EscalatedByEmail = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: false),
                    EscalatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    DisplayOrder = table.Column<int>(type: "int", nullable: false),
                    IsPublishedToBrief = table.Column<bool>(type: "bit", nullable: false),
                    PublishedToBriefId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
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
                    table.PrimaryKey("PK_ExecutiveQueueItems", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ExecutiveQueueItems_ExecutiveBriefs_PublishedToBriefId",
                        column: x => x.PublishedToBriefId,
                        principalTable: "ExecutiveBriefs",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_ExecutiveQueueItems_ExtractedArticles_ExtractedArticleId",
                        column: x => x.ExtractedArticleId,
                        principalTable: "ExtractedArticles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ExtractedArticles_ReviewState",
                table: "ExtractedArticles",
                column: "ReviewState");

            migrationBuilder.CreateIndex(
                name: "IX_ExtractedArticles_ReviewState_AssignedReviewerId",
                table: "ExtractedArticles",
                columns: new[] { "ReviewState", "AssignedReviewerId" });

            migrationBuilder.CreateIndex(
                name: "IX_ArticleAnnotations_ExtractedArticleId_CreatedAt",
                table: "ArticleAnnotations",
                columns: new[] { "ExtractedArticleId", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_ArticleReviewActions_ActorUserId_CreatedAt",
                table: "ArticleReviewActions",
                columns: new[] { "ActorUserId", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_ArticleReviewActions_ExtractedArticleId_CreatedAt",
                table: "ArticleReviewActions",
                columns: new[] { "ExtractedArticleId", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_ArticleReviewComments_ExtractedArticleId_CreatedAt",
                table: "ArticleReviewComments",
                columns: new[] { "ExtractedArticleId", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_ArticleReviewComments_ParentCommentId",
                table: "ArticleReviewComments",
                column: "ParentCommentId");

            migrationBuilder.CreateIndex(
                name: "IX_ExecutiveBriefItems_ExecutiveBriefId_DisplayOrder",
                table: "ExecutiveBriefItems",
                columns: new[] { "ExecutiveBriefId", "DisplayOrder" });

            migrationBuilder.CreateIndex(
                name: "IX_ExecutiveBriefItems_ExecutiveBriefId_ExtractedArticleId",
                table: "ExecutiveBriefItems",
                columns: new[] { "ExecutiveBriefId", "ExtractedArticleId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ExecutiveBriefItems_ExtractedArticleId",
                table: "ExecutiveBriefItems",
                column: "ExtractedArticleId");

            migrationBuilder.CreateIndex(
                name: "IX_ExecutiveBriefs_Status_PublishedAt",
                table: "ExecutiveBriefs",
                columns: new[] { "Status", "PublishedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_ExecutiveQueueItems_ExtractedArticleId",
                table: "ExecutiveQueueItems",
                column: "ExtractedArticleId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ExecutiveQueueItems_Priority_DisplayOrder",
                table: "ExecutiveQueueItems",
                columns: new[] { "Priority", "DisplayOrder" });

            migrationBuilder.CreateIndex(
                name: "IX_ExecutiveQueueItems_PublishedToBriefId",
                table: "ExecutiveQueueItems",
                column: "PublishedToBriefId");

            migrationBuilder.CreateIndex(
                name: "IX_ReviewAssignments_AssigneeUserId_Status",
                table: "ReviewAssignments",
                columns: new[] { "AssigneeUserId", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_ReviewAssignments_ExtractedArticleId_Status",
                table: "ReviewAssignments",
                columns: new[] { "ExtractedArticleId", "Status" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ArticleAnnotations");

            migrationBuilder.DropTable(
                name: "ArticleReviewActions");

            migrationBuilder.DropTable(
                name: "ArticleReviewComments");

            migrationBuilder.DropTable(
                name: "ExecutiveBriefItems");

            migrationBuilder.DropTable(
                name: "ExecutiveQueueItems");

            migrationBuilder.DropTable(
                name: "ReviewAssignments");

            migrationBuilder.DropTable(
                name: "ExecutiveBriefs");

            migrationBuilder.DropIndex(
                name: "IX_ExtractedArticles_ReviewState",
                table: "ExtractedArticles");

            migrationBuilder.DropIndex(
                name: "IX_ExtractedArticles_ReviewState_AssignedReviewerId",
                table: "ExtractedArticles");

            migrationBuilder.DropColumn(
                name: "AnalystHeadline",
                table: "ExtractedArticles");

            migrationBuilder.DropColumn(
                name: "AnalystRelevanceScore",
                table: "ExtractedArticles");

            migrationBuilder.DropColumn(
                name: "AnalystSentiment",
                table: "ExtractedArticles");

            migrationBuilder.DropColumn(
                name: "AnalystSummary",
                table: "ExtractedArticles");

            migrationBuilder.DropColumn(
                name: "AnalystTagsJson",
                table: "ExtractedArticles");

            migrationBuilder.DropColumn(
                name: "AssignedReviewerId",
                table: "ExtractedArticles");

            migrationBuilder.DropColumn(
                name: "LastReviewActionAt",
                table: "ExtractedArticles");

            migrationBuilder.DropColumn(
                name: "ReviewSlaDueAt",
                table: "ExtractedArticles");

            migrationBuilder.DropColumn(
                name: "ReviewState",
                table: "ExtractedArticles");
        }
    }
}
