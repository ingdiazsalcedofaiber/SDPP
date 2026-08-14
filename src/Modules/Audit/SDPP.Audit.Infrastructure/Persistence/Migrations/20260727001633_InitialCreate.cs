using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SDPP.Audit.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class InitialCreate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(
                name: "audit");

            migrationBuilder.CreateTable(
                name: "AuditRecords",
                schema: "audit",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    RecordGuid = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    OccurredAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    EventType = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    ActorUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ActorFullName = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    ActorEmail = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    ActorDomain = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    ActorIp = table.Column<string>(type: "nvarchar(45)", maxLength: 45, nullable: true),
                    ActorMac = table.Column<string>(type: "nvarchar(17)", maxLength: 17, nullable: true),
                    ActorHostname = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    ActorOs = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    ActorUserAgent = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    SubjectDocumentId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    PayloadJson = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    PreviousRecordHash = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    RecordHash = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AuditRecords", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_AuditRecords_ActorUserId_OccurredAtUtc",
                schema: "audit",
                table: "AuditRecords",
                columns: new[] { "ActorUserId", "OccurredAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_AuditRecords_SubjectDocumentId_OccurredAtUtc",
                schema: "audit",
                table: "AuditRecords",
                columns: new[] { "SubjectDocumentId", "OccurredAtUtc" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AuditRecords",
                schema: "audit");
        }
    }
}
