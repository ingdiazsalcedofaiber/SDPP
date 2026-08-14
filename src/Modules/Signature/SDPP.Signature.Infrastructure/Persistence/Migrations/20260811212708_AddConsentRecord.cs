using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SDPP.Signature.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddConsentRecord : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ConsentRecords",
                schema: "signature",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    EnvelopeId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    RecipientId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ConsentText = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: false),
                    ConsentVersion = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    TimestampUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    IpAddress = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: true),
                    UserAgent = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    AuthenticationMethod = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ConsentRecords", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ConsentRecords_SignatureEnvelopes_EnvelopeId",
                        column: x => x.EnvelopeId,
                        principalSchema: "signature",
                        principalTable: "SignatureEnvelopes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ConsentRecords_EnvelopeId",
                schema: "signature",
                table: "ConsentRecords",
                column: "EnvelopeId");

            migrationBuilder.CreateIndex(
                name: "IX_ConsentRecords_RecipientId",
                schema: "signature",
                table: "ConsentRecords",
                column: "RecipientId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ConsentRecords",
                schema: "signature");
        }
    }
}
