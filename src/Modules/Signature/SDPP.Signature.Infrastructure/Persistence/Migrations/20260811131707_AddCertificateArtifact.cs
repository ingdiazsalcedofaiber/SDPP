using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SDPP.Signature.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddCertificateArtifact : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<byte[]>(
                name: "CertificateDocument",
                schema: "signature",
                table: "SignatureEnvelopes",
                type: "varbinary(max)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "CertificateHash",
                schema: "signature",
                table: "SignatureEnvelopes",
                type: "nvarchar(64)",
                maxLength: 64,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "CertificateDocument",
                schema: "signature",
                table: "SignatureEnvelopes");

            migrationBuilder.DropColumn(
                name: "CertificateHash",
                schema: "signature",
                table: "SignatureEnvelopes");
        }
    }
}
