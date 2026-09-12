using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Fidellis.Infrastructure.Migrations.Tenant
{
    /// <inheritdoc />
    public partial class DropLegacyLabels : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "onetime_label",
                table: "finance_settings");

            migrationBuilder.DropColumn(
                name: "recurring_label",
                table: "finance_settings");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "onetime_label",
                table: "finance_settings",
                type: "text",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "recurring_label",
                table: "finance_settings",
                type: "text",
                nullable: false,
                defaultValue: "");
        }
    }
}
