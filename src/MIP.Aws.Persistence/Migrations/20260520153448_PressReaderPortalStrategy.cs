using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MIP.Aws.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class PressReaderPortalStrategy : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "ContextMenuSelector",
                table: "NewsSources",
                type: "nvarchar(512)",
                maxLength: 512,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "DownloadMenuItemSelector",
                table: "NewsSources",
                type: "nvarchar(512)",
                maxLength: 512,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "DownloadWaitTimeoutSeconds",
                table: "NewsSources",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<string>(
                name: "LoginIconSelector",
                table: "NewsSources",
                type: "nvarchar(512)",
                maxLength: 512,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "NewspaperCanvasSelector",
                table: "NewsSources",
                type: "nvarchar(512)",
                maxLength: 512,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "PortalStrategyKey",
                table: "NewsSources",
                type: "nvarchar(64)",
                maxLength: 64,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ContextMenuSelector",
                table: "NewsSources");

            migrationBuilder.DropColumn(
                name: "DownloadMenuItemSelector",
                table: "NewsSources");

            migrationBuilder.DropColumn(
                name: "DownloadWaitTimeoutSeconds",
                table: "NewsSources");

            migrationBuilder.DropColumn(
                name: "LoginIconSelector",
                table: "NewsSources");

            migrationBuilder.DropColumn(
                name: "NewspaperCanvasSelector",
                table: "NewsSources");

            migrationBuilder.DropColumn(
                name: "PortalStrategyKey",
                table: "NewsSources");
        }
    }
}
