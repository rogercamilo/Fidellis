using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Fidellis.Infrastructure.Migrations.Tenant
{
    /// <inheritdoc />
    public partial class RoleLabels : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Rótulos de papéis por tenant (D-02 Q6): overrides como JSON; existentes recebem '{}' (sem overrides).
            migrationBuilder.AddColumn<string>(
                name: "role_labels_json",
                table: "finance_settings",
                type: "text",
                nullable: false,
                defaultValue: "{}");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "role_labels_json",
                table: "finance_settings");
        }
    }
}
