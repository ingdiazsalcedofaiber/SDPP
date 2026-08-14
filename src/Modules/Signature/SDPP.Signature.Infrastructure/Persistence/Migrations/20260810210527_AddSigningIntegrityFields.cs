using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SDPP.Signature.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddSigningIntegrityFields : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "EnvelopeHash",
                schema: "signature",
                table: "SignatureEnvelopes",
                type: "nvarchar(64)",
                maxLength: 64,
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "ConsentAcceptedAtUtc",
                schema: "signature",
                table: "EnvelopeRecipients",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ConsentIpAddress",
                schema: "signature",
                table: "EnvelopeRecipients",
                type: "nvarchar(64)",
                maxLength: 64,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ConsentUserAgent",
                schema: "signature",
                table: "EnvelopeRecipients",
                type: "nvarchar(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "SignatureHash",
                schema: "signature",
                table: "EnvelopeFields",
                type: "nvarchar(64)",
                maxLength: 64,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "EnvelopeHash",
                schema: "signature",
                table: "SignatureEnvelopes");

            migrationBuilder.DropColumn(
                name: "ConsentAcceptedAtUtc",
                schema: "signature",
                table: "EnvelopeRecipients");

            migrationBuilder.DropColumn(
                name: "ConsentIpAddress",
                schema: "signature",
                table: "EnvelopeRecipients");

            migrationBuilder.DropColumn(
                name: "ConsentUserAgent",
                schema: "signature",
                table: "EnvelopeRecipients");

            migrationBuilder.DropColumn(
                name: "SignatureHash",
                schema: "signature",
                table: "EnvelopeFields");
        }
    }
}
