using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Fidellis.Infrastructure.Migrations.Tenant
{
    /// <inheritdoc />
    public partial class RemapCouncilRoles : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // D-02 §7.2: reescreve os RolesCsv das faixas default para o vocabulário de conselho — o
            // conselho fiscal SAI da faixa alta (deixa de autorizar); entra o moderador/presidente.
            // Roda no schema de cada tenant (search_path); só toca as faixas com os valores default.
            migrationBuilder.Sql("UPDATE approval_tiers SET roles_csv = 'coordinator,council_officer' WHERE roles_csv = 'treasurer';");
            migrationBuilder.Sql("UPDATE approval_tiers SET roles_csv = 'coordinator,council_officer' WHERE roles_csv = 'treasurer,manager';");
            migrationBuilder.Sql("UPDATE approval_tiers SET roles_csv = 'council_officer,council_chair' WHERE roles_csv = 'manager,fiscal_council';");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("UPDATE approval_tiers SET roles_csv = 'treasurer' WHERE roles_csv = 'coordinator,council_officer' AND min_amount = 0;");
            migrationBuilder.Sql("UPDATE approval_tiers SET roles_csv = 'treasurer,manager' WHERE roles_csv = 'coordinator,council_officer' AND min_amount = 500;");
            migrationBuilder.Sql("UPDATE approval_tiers SET roles_csv = 'manager,fiscal_council' WHERE roles_csv = 'council_officer,council_chair';");
        }
    }
}
