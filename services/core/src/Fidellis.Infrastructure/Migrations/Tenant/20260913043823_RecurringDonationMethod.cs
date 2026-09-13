using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Fidellis.Infrastructure.Migrations.Tenant
{
    /// <inheritdoc />
    public partial class RecurringDonationMethod : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "method",
                table: "recurring_donations",
                type: "text",
                nullable: false,
                defaultValue: "");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "method",
                table: "recurring_donations");
        }
    }
}
