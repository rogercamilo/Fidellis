using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Fidellis.Infrastructure.Migrations.Tenant
{
    /// <inheritdoc />
    public partial class EntryTypeFirstClass : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Compromissos recorrentes existentes eram dízimo (inferência antiga).
            migrationBuilder.AddColumn<string>(
                name: "entry_type",
                table: "recurring_donations",
                type: "text",
                nullable: false,
                defaultValue: "tithe");

            // Rótulos por tipo (D-06): defaults PT-BR, customizáveis por tenant.
            migrationBuilder.AddColumn<string>(
                name: "donation_label",
                table: "finance_settings",
                type: "text",
                nullable: false,
                defaultValue: "Doação");

            migrationBuilder.AddColumn<string>(
                name: "offering_label",
                table: "finance_settings",
                type: "text",
                nullable: false,
                defaultValue: "Oferta");

            migrationBuilder.AddColumn<string>(
                name: "tithe_label",
                table: "finance_settings",
                type: "text",
                nullable: false,
                defaultValue: "Dízimo");

            // Entradas existentes: neutro 'donation'; backfill abaixo espelha a inferência antiga.
            migrationBuilder.AddColumn<string>(
                name: "entry_type",
                table: "donations",
                type: "text",
                nullable: false,
                defaultValue: "donation");

            // Backfill (D-06 §3): recorrente ⇒ dízimo; coleta em espécie (D-05) ⇒ oferta.
            migrationBuilder.Sql("UPDATE donations SET entry_type = 'tithe' WHERE recurring_donation_id IS NOT NULL;");
            migrationBuilder.Sql("UPDATE donations SET entry_type = 'offering' WHERE source = 'cash';");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "entry_type",
                table: "recurring_donations");

            migrationBuilder.DropColumn(
                name: "donation_label",
                table: "finance_settings");

            migrationBuilder.DropColumn(
                name: "offering_label",
                table: "finance_settings");

            migrationBuilder.DropColumn(
                name: "tithe_label",
                table: "finance_settings");

            migrationBuilder.DropColumn(
                name: "entry_type",
                table: "donations");
        }
    }
}
