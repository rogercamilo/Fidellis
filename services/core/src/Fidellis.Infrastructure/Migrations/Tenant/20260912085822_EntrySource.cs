using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Fidellis.Infrastructure.Migrations.Tenant
{
    /// <inheritdoc />
    public partial class EntrySource : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Entradas existentes (via PSP) recebem a origem 'checkout'.
            migrationBuilder.AddColumn<string>(
                name: "source",
                table: "donations",
                type: "text",
                nullable: false,
                defaultValue: "checkout");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "source",
                table: "donations");
        }
    }
}
