using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SDPP.Identity.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddMfa : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "MfaEnabled",
                schema: "identity",
                table: "Users",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<DateTime>(
                name: "MfaEnrolledAtUtc",
                schema: "identity",
                table: "Users",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "MfaSecretEncrypted",
                schema: "identity",
                table: "Users",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "MfaBackupCodes",
                schema: "identity",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    UserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CodeHash = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UsedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MfaBackupCodes", x => x.Id);
                    table.ForeignKey(
                        name: "FK_MfaBackupCodes_Users_UserId",
                        column: x => x.UserId,
                        principalSchema: "identity",
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "MfaChallenges",
                schema: "identity",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    UserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TokenHash = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    Purpose = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    ExpiresAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    ConsumedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    FailedAttempts = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MfaChallenges", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_MfaBackupCodes_UserId_CodeHash",
                schema: "identity",
                table: "MfaBackupCodes",
                columns: new[] { "UserId", "CodeHash" });

            migrationBuilder.CreateIndex(
                name: "IX_MfaChallenges_TokenHash",
                schema: "identity",
                table: "MfaChallenges",
                column: "TokenHash",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_MfaChallenges_UserId_ExpiresAtUtc",
                schema: "identity",
                table: "MfaChallenges",
                columns: new[] { "UserId", "ExpiresAtUtc" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "MfaBackupCodes",
                schema: "identity");

            migrationBuilder.DropTable(
                name: "MfaChallenges",
                schema: "identity");

            migrationBuilder.DropColumn(
                name: "MfaEnabled",
                schema: "identity",
                table: "Users");

            migrationBuilder.DropColumn(
                name: "MfaEnrolledAtUtc",
                schema: "identity",
                table: "Users");

            migrationBuilder.DropColumn(
                name: "MfaSecretEncrypted",
                schema: "identity",
                table: "Users");
        }
    }
}
