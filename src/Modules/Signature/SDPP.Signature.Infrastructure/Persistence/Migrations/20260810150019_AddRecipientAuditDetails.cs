using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SDPP.Signature.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddRecipientAuditDetails : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "AuthMethodUsed",
                schema: "signature",
                table: "EnvelopeRecipients",
                type: "nvarchar(20)",
                maxLength: 20,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "SignedIpAddress",
                schema: "signature",
                table: "EnvelopeRecipients",
                type: "nvarchar(64)",
                maxLength: 64,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ViewedIpAddress",
                schema: "signature",
                table: "EnvelopeRecipients",
                type: "nvarchar(64)",
                maxLength: 64,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "AuthMethodUsed",
                schema: "signature",
                table: "EnvelopeRecipients");

            migrationBuilder.DropColumn(
                name: "SignedIpAddress",
                schema: "signature",
                table: "EnvelopeRecipients");

            migrationBuilder.DropColumn(
                name: "ViewedIpAddress",
                schema: "signature",
                table: "EnvelopeRecipients");
        }
    }
}
