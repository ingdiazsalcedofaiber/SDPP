using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SDPP.Signature.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddNotificationsAndReminders : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "LastReminderSentAtUtc",
                schema: "signature",
                table: "EnvelopeRecipients",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "ReminderCount",
                schema: "signature",
                table: "EnvelopeRecipients",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.CreateTable(
                name: "Notifications",
                schema: "signature",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    UserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Type = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false),
                    Title = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Message = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: false),
                    EnvelopeId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    ReadAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Notifications", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Notifications_ReadAtUtc",
                schema: "signature",
                table: "Notifications",
                column: "ReadAtUtc");

            migrationBuilder.CreateIndex(
                name: "IX_Notifications_UserId",
                schema: "signature",
                table: "Notifications",
                column: "UserId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "Notifications",
                schema: "signature");

            migrationBuilder.DropColumn(
                name: "LastReminderSentAtUtc",
                schema: "signature",
                table: "EnvelopeRecipients");

            migrationBuilder.DropColumn(
                name: "ReminderCount",
                schema: "signature",
                table: "EnvelopeRecipients");
        }
    }
}
