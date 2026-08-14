using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SDPP.Signature.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddCryptographicSignature : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "DocumentSignatures",
                schema: "signature",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    EnvelopeId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    RecipientId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    DocumentId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    DocumentVersionId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    DocumentHashAtSigning = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    CanonicalPayloadHash = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    CryptographicSignatureBase64 = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    PublicKeyId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Algorithm = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    ConsentId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    TimestampUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    TimestampSource = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DocumentSignatures", x => x.Id);
                    table.ForeignKey(
                        name: "FK_DocumentSignatures_SignatureEnvelopes_EnvelopeId",
                        column: x => x.EnvelopeId,
                        principalSchema: "signature",
                        principalTable: "SignatureEnvelopes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "SignatureKeys",
                schema: "signature",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Algorithm = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    PublicKeyBase64 = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    EncryptedPrivateKey = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    RevokedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    Status = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SignatureKeys", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_DocumentSignatures_EnvelopeId",
                schema: "signature",
                table: "DocumentSignatures",
                column: "EnvelopeId");

            migrationBuilder.CreateIndex(
                name: "IX_DocumentSignatures_PublicKeyId",
                schema: "signature",
                table: "DocumentSignatures",
                column: "PublicKeyId");

            migrationBuilder.CreateIndex(
                name: "IX_DocumentSignatures_RecipientId",
                schema: "signature",
                table: "DocumentSignatures",
                column: "RecipientId");

            migrationBuilder.CreateIndex(
                name: "IX_SignatureKeys_Status",
                schema: "signature",
                table: "SignatureKeys",
                column: "Status");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "DocumentSignatures",
                schema: "signature");

            migrationBuilder.DropTable(
                name: "SignatureKeys",
                schema: "signature");
        }
    }
}
