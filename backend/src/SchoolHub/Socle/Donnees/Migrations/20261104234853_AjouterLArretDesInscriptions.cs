using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SchoolHub.Socle.Donnees.Migrations
{
    /// <inheritdoc />
    public partial class AjouterLArretDesInscriptions : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "ix_inscriptions_eleve_id_groupe_id",
                table: "inscriptions");

            migrationBuilder.AddColumn<DateOnly>(
                name: "arrete_le",
                table: "inscriptions",
                type: "date",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "report",
                table: "inscriptions",
                type: "numeric(12,3)",
                precision: 12,
                scale: 3,
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.CreateIndex(
                name: "ix_inscriptions_eleve_id_groupe_id",
                table: "inscriptions",
                columns: new[] { "eleve_id", "groupe_id" },
                unique: true,
                filter: "arrete_le IS NULL");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "ix_inscriptions_eleve_id_groupe_id",
                table: "inscriptions");

            migrationBuilder.DropColumn(
                name: "arrete_le",
                table: "inscriptions");

            migrationBuilder.DropColumn(
                name: "report",
                table: "inscriptions");

            migrationBuilder.CreateIndex(
                name: "ix_inscriptions_eleve_id_groupe_id",
                table: "inscriptions",
                columns: new[] { "eleve_id", "groupe_id" },
                unique: true);
        }
    }
}
