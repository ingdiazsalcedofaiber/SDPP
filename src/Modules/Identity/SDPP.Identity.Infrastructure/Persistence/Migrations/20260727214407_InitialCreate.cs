using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SDPP.Identity.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class InitialCreate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(
                name: "identity");

            migrationBuilder.CreateTable(
                name: "AccessAuditLog",
                schema: "identity",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    UserId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    FullNameSnapshot = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Email = table.Column<string>(type: "nvarchar(320)", maxLength: 320, nullable: false),
                    OccurredAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    IpAddress = table.Column<string>(type: "nvarchar(45)", maxLength: 45, nullable: true),
                    UserAgent = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    Result = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false),
                    Provider = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false),
                    DurationMs = table.Column<int>(type: "int", nullable: false),
                    SessionId = table.Column<Guid>(type: "uniqueidentifier", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AccessAuditLog", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "AllowedDomains",
                schema: "identity",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Domain = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: false),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    IsDevOnly = table.Column<bool>(type: "bit", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    Notes = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AllowedDomains", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "OutboxMessages",
                schema: "identity",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    OccurredAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    Type = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                    Content = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    ProcessedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    Error = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    AttemptCount = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_OutboxMessages", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Roles",
                schema: "identity",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    Description = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: true),
                    IsSystemRole = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Roles", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Sessions",
                schema: "identity",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    UserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    RefreshTokenHash = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    IpAddress = table.Column<string>(type: "nvarchar(45)", maxLength: 45, nullable: true),
                    UserAgent = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    OperatingSystem = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    ExpiresAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    RevokedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    LastUsedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Sessions", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Users",
                schema: "identity",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    GoogleId = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: false),
                    FullName = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Email = table.Column<string>(type: "nvarchar(320)", maxLength: 320, nullable: false),
                    PhotoUrl = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    Domain = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: false),
                    Department = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    EmailVerified = table.Column<bool>(type: "bit", nullable: false),
                    Active = table.Column<bool>(type: "bit", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    LastLoginAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    UpdatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Users", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "UserRoles",
                schema: "identity",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    UserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    RoleId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    AssignedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    AssignedByUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_UserRoles", x => x.Id);
                    table.ForeignKey(
                        name: "FK_UserRoles_Users_UserId",
                        column: x => x.UserId,
                        principalSchema: "identity",
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_AccessAuditLog_Email",
                schema: "identity",
                table: "AccessAuditLog",
                column: "Email");

            migrationBuilder.CreateIndex(
                name: "IX_AccessAuditLog_OccurredAtUtc",
                schema: "identity",
                table: "AccessAuditLog",
                column: "OccurredAtUtc");

            migrationBuilder.CreateIndex(
                name: "IX_AccessAuditLog_UserId_OccurredAtUtc",
                schema: "identity",
                table: "AccessAuditLog",
                columns: new[] { "UserId", "OccurredAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_AllowedDomains_Domain",
                schema: "identity",
                table: "AllowedDomains",
                column: "Domain",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Roles_Name",
                schema: "identity",
                table: "Roles",
                column: "Name",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Sessions_RefreshTokenHash",
                schema: "identity",
                table: "Sessions",
                column: "RefreshTokenHash",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Sessions_UserId_ExpiresAtUtc",
                schema: "identity",
                table: "Sessions",
                columns: new[] { "UserId", "ExpiresAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_UserRoles_UserId_RoleId",
                schema: "identity",
                table: "UserRoles",
                columns: new[] { "UserId", "RoleId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Users_Email",
                schema: "identity",
                table: "Users",
                column: "Email",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Users_GoogleId",
                schema: "identity",
                table: "Users",
                column: "GoogleId",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AccessAuditLog",
                schema: "identity");

            migrationBuilder.DropTable(
                name: "AllowedDomains",
                schema: "identity");

            migrationBuilder.DropTable(
                name: "OutboxMessages",
                schema: "identity");

            migrationBuilder.DropTable(
                name: "Roles",
                schema: "identity");

            migrationBuilder.DropTable(
                name: "Sessions",
                schema: "identity");

            migrationBuilder.DropTable(
                name: "UserRoles",
                schema: "identity");

            migrationBuilder.DropTable(
                name: "Users",
                schema: "identity");
        }
    }
}
