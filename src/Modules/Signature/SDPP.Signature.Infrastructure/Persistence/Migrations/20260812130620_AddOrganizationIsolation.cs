using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SDPP.Signature.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddOrganizationIsolation : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "OrganizationId",
                schema: "signature",
                table: "SignatureEnvelopes",
                type: "uniqueidentifier",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000001")); // DefaultOrganizationContextProvider.DefaultOrganizationId

            migrationBuilder.CreateIndex(
                name: "IX_SignatureEnvelopes_OrganizationId",
                schema: "signature",
                table: "SignatureEnvelopes",
                column: "OrganizationId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_SignatureEnvelopes_OrganizationId",
                schema: "signature",
                table: "SignatureEnvelopes");

            migrationBuilder.DropColumn(
                name: "OrganizationId",
                schema: "signature",
                table: "SignatureEnvelopes");
        }
    }
}
