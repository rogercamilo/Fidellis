using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Fidellis.Infrastructure.Migrations.Catalog
{
    /// <inheritdoc />
    public partial class RemapCouncilRoles : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // D-02 §7.1: renomeia os papéis das memberships para o vocabulário de conselho.
            migrationBuilder.Sql("UPDATE catalog.memberships SET role = 'coordinator' WHERE role = 'treasurer';");
            migrationBuilder.Sql("UPDATE catalog.memberships SET role = 'council_officer' WHERE role = 'manager';");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("UPDATE catalog.memberships SET role = 'treasurer' WHERE role = 'coordinator';");
            migrationBuilder.Sql("UPDATE catalog.memberships SET role = 'manager' WHERE role = 'council_officer';");
        }
    }
}
