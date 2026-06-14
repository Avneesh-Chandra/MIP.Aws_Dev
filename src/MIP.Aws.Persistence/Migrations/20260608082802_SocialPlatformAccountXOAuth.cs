using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MIP.Aws.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class SocialPlatformAccountXOAuth : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<string>(
                name: "OAuthState",
                table: "SocialPlatformAccounts",
                type: "nvarchar(256)",
                maxLength: 256,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "nvarchar(128)",
                oldMaxLength: 128,
                oldNullable: true);

            migrationBuilder.AddColumn<string>(
                name: "AccountEmail",
                table: "SocialPlatformAccounts",
                type: "nvarchar(256)",
                maxLength: 256,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ConnectionStatus",
                table: "SocialPlatformAccounts",
                type: "nvarchar(64)",
                maxLength: 64,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "EnvironmentName",
                table: "SocialPlatformAccounts",
                type: "nvarchar(64)",
                maxLength: 64,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Handle",
                table: "SocialPlatformAccounts",
                type: "nvarchar(128)",
                maxLength: 128,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "LastConnectionError",
                table: "SocialPlatformAccounts",
                type: "nvarchar(2000)",
                maxLength: 2000,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Scopes",
                table: "SocialPlatformAccounts",
                type: "nvarchar(512)",
                maxLength: 512,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "AccountEmail",
                table: "SocialPlatformAccounts");

            migrationBuilder.DropColumn(
                name: "ConnectionStatus",
                table: "SocialPlatformAccounts");

            migrationBuilder.DropColumn(
                name: "EnvironmentName",
                table: "SocialPlatformAccounts");

            migrationBuilder.DropColumn(
                name: "Handle",
                table: "SocialPlatformAccounts");

            migrationBuilder.DropColumn(
                name: "LastConnectionError",
                table: "SocialPlatformAccounts");

            migrationBuilder.DropColumn(
                name: "Scopes",
                table: "SocialPlatformAccounts");

            migrationBuilder.AlterColumn<string>(
                name: "OAuthState",
                table: "SocialPlatformAccounts",
                type: "nvarchar(128)",
                maxLength: 128,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "nvarchar(256)",
                oldMaxLength: 256,
                oldNullable: true);
        }
    }
}
