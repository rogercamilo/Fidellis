using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Fidellis.Infrastructure.Migrations.Tenant
{
    /// <inheritdoc />
    public partial class RenameDonationToEntry : Migration
    {
        // No-op de schema (#74): a entidade C# Donation foi renomeada para Entry, mas a tabela permanece
        // "donations" (via ToTable). Esta migração só re-sincroniza o ModelSnapshot com o novo nome.
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
        }
    }
}
