using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Fidellis.Infrastructure.Migrations.Tenant
{
    /// <inheritdoc />
    public partial class DonorFederatedIdentity : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "external_id",
                table: "donors",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "source",
                table: "donors",
                type: "text",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "ix_donors_source_external_id",
                table: "donors",
                columns: new[] { "source", "external_id" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "ix_donors_source_external_id",
                table: "donors");

            migrationBuilder.DropColumn(
                name: "external_id",
                table: "donors");

            migrationBuilder.DropColumn(
                name: "source",
                table: "donors");
        }
    }
}
