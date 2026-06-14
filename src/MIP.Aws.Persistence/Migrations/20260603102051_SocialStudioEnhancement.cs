using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MIP.Aws.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class SocialStudioEnhancement : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_SocialPostPlatforms_SocialPostId",
                table: "SocialPostPlatforms");

            migrationBuilder.AddColumn<decimal>(
                name: "AiConfidence",
                table: "SocialPosts",
                type: "decimal(18,2)",
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "AiGeneratedAt",
                table: "SocialPosts",
                type: "datetimeoffset",
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "ComplianceCheckedAt",
                table: "SocialPosts",
                type: "datetimeoffset",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ComplianceIssuesJson",
                table: "SocialPosts",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ComplianceRiskLevel",
                table: "SocialPosts",
                type: "nvarchar(32)",
                maxLength: 32,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "ComplianceScore",
                table: "SocialPosts",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "ExtractedArticleId",
                table: "SocialPosts",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "SourceType",
                table: "SocialPosts",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<decimal>(
                name: "AiConfidence",
                table: "SocialPostPlatforms",
                type: "decimal(18,2)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "CallToAction",
                table: "SocialPostPlatforms",
                type: "nvarchar(512)",
                maxLength: 512,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ComplianceNotes",
                table: "SocialPostPlatforms",
                type: "nvarchar(4000)",
                maxLength: 4000,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "ContentVariant",
                table: "SocialPostPlatforms",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<string>(
                name: "HashtagsJson",
                table: "SocialPostPlatforms",
                type: "nvarchar(2000)",
                maxLength: 2000,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Headline",
                table: "SocialPostPlatforms",
                type: "nvarchar(512)",
                maxLength: 512,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "MentionsJson",
                table: "SocialPostPlatforms",
                type: "nvarchar(2000)",
                maxLength: 2000,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "AttachmentKind",
                table: "SocialPostAttachments",
                type: "nvarchar(64)",
                maxLength: 64,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "PreviewUrl",
                table: "SocialPostAttachments",
                type: "nvarchar(1024)",
                maxLength: 1024,
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_SocialPosts_ExtractedArticleId",
                table: "SocialPosts",
                column: "ExtractedArticleId");

            // Legacy rows received ContentVariant default 0; map by platform then de-duplicate before unique index.
            migrationBuilder.Sql("""
                UPDATE SocialPostPlatforms
                SET ContentVariant = CASE Platform WHEN 1 THEN 2 WHEN 2 THEN 4 ELSE 0 END;
                """);

            migrationBuilder.Sql("""
                WITH numbered AS (
                    SELECT Id, ROW_NUMBER() OVER (PARTITION BY SocialPostId, ContentVariant ORDER BY Id) AS rn
                    FROM SocialPostPlatforms
                )
                UPDATE p
                SET ContentVariant = p.ContentVariant + (n.rn - 1)
                FROM SocialPostPlatforms p
                INNER JOIN numbered n ON p.Id = n.Id
                WHERE n.rn > 1;
                """);

            migrationBuilder.CreateIndex(
                name: "IX_SocialPostPlatforms_SocialPostId_ContentVariant",
                table: "SocialPostPlatforms",
                columns: new[] { "SocialPostId", "ContentVariant" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_SocialPosts_ExtractedArticleId",
                table: "SocialPosts");

            migrationBuilder.DropIndex(
                name: "IX_SocialPostPlatforms_SocialPostId_ContentVariant",
                table: "SocialPostPlatforms");

            migrationBuilder.DropColumn(
                name: "AiConfidence",
                table: "SocialPosts");

            migrationBuilder.DropColumn(
                name: "AiGeneratedAt",
                table: "SocialPosts");

            migrationBuilder.DropColumn(
                name: "ComplianceCheckedAt",
                table: "SocialPosts");

            migrationBuilder.DropColumn(
                name: "ComplianceIssuesJson",
                table: "SocialPosts");

            migrationBuilder.DropColumn(
                name: "ComplianceRiskLevel",
                table: "SocialPosts");

            migrationBuilder.DropColumn(
                name: "ComplianceScore",
                table: "SocialPosts");

            migrationBuilder.DropColumn(
                name: "ExtractedArticleId",
                table: "SocialPosts");

            migrationBuilder.DropColumn(
                name: "SourceType",
                table: "SocialPosts");

            migrationBuilder.DropColumn(
                name: "AiConfidence",
                table: "SocialPostPlatforms");

            migrationBuilder.DropColumn(
                name: "CallToAction",
                table: "SocialPostPlatforms");

            migrationBuilder.DropColumn(
                name: "ComplianceNotes",
                table: "SocialPostPlatforms");

            migrationBuilder.DropColumn(
                name: "ContentVariant",
                table: "SocialPostPlatforms");

            migrationBuilder.DropColumn(
                name: "HashtagsJson",
                table: "SocialPostPlatforms");

            migrationBuilder.DropColumn(
                name: "Headline",
                table: "SocialPostPlatforms");

            migrationBuilder.DropColumn(
                name: "MentionsJson",
                table: "SocialPostPlatforms");

            migrationBuilder.DropColumn(
                name: "AttachmentKind",
                table: "SocialPostAttachments");

            migrationBuilder.DropColumn(
                name: "PreviewUrl",
                table: "SocialPostAttachments");

            migrationBuilder.CreateIndex(
                name: "IX_SocialPostPlatforms_SocialPostId",
                table: "SocialPostPlatforms",
                column: "SocialPostId");
        }
    }
}
