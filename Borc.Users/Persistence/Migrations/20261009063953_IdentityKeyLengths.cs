using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Borc.Users.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class IdentityKeyLengths : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // SQL Server cannot alter a column that is part of a primary key: drop the keys, alter, recreate.
            migrationBuilder.DropPrimaryKey(name: "PK_UserTokens", schema: "usr", table: "UserTokens");
            migrationBuilder.DropPrimaryKey(name: "PK_UserLogins", schema: "usr", table: "UserLogins");

            migrationBuilder.AlterColumn<string>(
                name: "Name",
                schema: "usr",
                table: "UserTokens",
                type: "nvarchar(128)",
                maxLength: 128,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(450)");

            migrationBuilder.AlterColumn<string>(
                name: "LoginProvider",
                schema: "usr",
                table: "UserTokens",
                type: "nvarchar(128)",
                maxLength: 128,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(450)");

            migrationBuilder.AlterColumn<string>(
                name: "ProviderKey",
                schema: "usr",
                table: "UserLogins",
                type: "nvarchar(128)",
                maxLength: 128,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(450)");

            migrationBuilder.AlterColumn<string>(
                name: "LoginProvider",
                schema: "usr",
                table: "UserLogins",
                type: "nvarchar(128)",
                maxLength: 128,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(450)");

            migrationBuilder.AddPrimaryKey(name: "PK_UserLogins", schema: "usr", table: "UserLogins", columns: new[] { "LoginProvider", "ProviderKey" });
            migrationBuilder.AddPrimaryKey(name: "PK_UserTokens", schema: "usr", table: "UserTokens", columns: new[] { "UserId", "LoginProvider", "Name" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // SQL Server cannot alter a column that is part of a primary key: drop the keys, alter, recreate.
            migrationBuilder.DropPrimaryKey(name: "PK_UserTokens", schema: "usr", table: "UserTokens");
            migrationBuilder.DropPrimaryKey(name: "PK_UserLogins", schema: "usr", table: "UserLogins");

            migrationBuilder.AlterColumn<string>(
                name: "Name",
                schema: "usr",
                table: "UserTokens",
                type: "nvarchar(450)",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(128)",
                oldMaxLength: 128);

            migrationBuilder.AlterColumn<string>(
                name: "LoginProvider",
                schema: "usr",
                table: "UserTokens",
                type: "nvarchar(450)",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(128)",
                oldMaxLength: 128);

            migrationBuilder.AlterColumn<string>(
                name: "ProviderKey",
                schema: "usr",
                table: "UserLogins",
                type: "nvarchar(450)",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(128)",
                oldMaxLength: 128);

            migrationBuilder.AlterColumn<string>(
                name: "LoginProvider",
                schema: "usr",
                table: "UserLogins",
                type: "nvarchar(450)",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(128)",
                oldMaxLength: 128);

            migrationBuilder.AddPrimaryKey(name: "PK_UserLogins", schema: "usr", table: "UserLogins", columns: new[] { "LoginProvider", "ProviderKey" });
            migrationBuilder.AddPrimaryKey(name: "PK_UserTokens", schema: "usr", table: "UserTokens", columns: new[] { "UserId", "LoginProvider", "Name" });
        }
    }
}
