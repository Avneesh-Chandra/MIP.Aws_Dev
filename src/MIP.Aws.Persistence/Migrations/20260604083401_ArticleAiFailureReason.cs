using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MIP.Aws.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class ArticleAiFailureReason : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "AiLastFailureDetail",
                table: "ExtractedArticles",
                type: "nvarchar(512)",
                maxLength: 512,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "AiLastFailureReason",
                table: "ExtractedArticles",
                type: "int",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "AiLastFailureDetail",
                table: "ExtractedArticles");

            migrationBuilder.DropColumn(
                name: "AiLastFailureReason",
                table: "ExtractedArticles");
        }
    }
}
