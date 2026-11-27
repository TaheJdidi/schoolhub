using System;
using Microsoft.EntityFrameworkCore.Migrations;
using SchoolHub.Modules.Abonnements;

#nullable disable

namespace SchoolHub.Socle.Donnees.Migrations
{
    /// <inheritdoc />
    public partial class SuiteDuBackOffice : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateOnly>(
                name: "echeance",
                table: "envois_au_proprietaire",
                type: "date",
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "support_autorise_jusqu_au",
                table: "abonnements",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "ix_envois_une_relance_automatique_par_echeance",
                table: "envois_au_proprietaire",
                columns: new[] { "centre_id", "objet", "echeance" },
                unique: true,
                filter: "objet IN ('RelanceAvantLaFin', 'RelanceDeLaFin')");

            // Le journal d'un centre ne s'efface plus qu'avec son centre, 12 mois après sa suspension (EDI-08).
            migrationBuilder.Sql(SuppressionDUnCentre.SqlDeLaProtection);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(SuppressionDUnCentre.SqlDuRetour);

            migrationBuilder.DropIndex(
                name: "ix_envois_une_relance_automatique_par_echeance",
                table: "envois_au_proprietaire");

            migrationBuilder.DropColumn(
                name: "echeance",
                table: "envois_au_proprietaire");

            migrationBuilder.DropColumn(
                name: "support_autorise_jusqu_au",
                table: "abonnements");
        }
    }
}
