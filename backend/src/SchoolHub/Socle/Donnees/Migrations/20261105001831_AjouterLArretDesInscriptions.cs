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

            migrationBuilder.AddColumn<decimal>(
                name: "part_du_report",
                table: "reglements",
                type: "numeric(12,3)",
                precision: 12,
                scale: 3,
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<DateOnly>(
                name: "arrete_le",
                table: "inscriptions",
                type: "date",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "depuis_inscription_id",
                table: "inscriptions",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "ix_inscriptions_depuis_inscription_id",
                table: "inscriptions",
                column: "depuis_inscription_id");

            migrationBuilder.CreateIndex(
                name: "ix_inscriptions_eleve_id_groupe_id",
                table: "inscriptions",
                columns: new[] { "eleve_id", "groupe_id" },
                unique: true,
                filter: "arrete_le IS NULL");

            migrationBuilder.AddForeignKey(
                name: "fk_inscriptions_inscriptions_depuis_inscription_id",
                table: "inscriptions",
                column: "depuis_inscription_id",
                principalTable: "inscriptions",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "fk_inscriptions_inscriptions_depuis_inscription_id",
                table: "inscriptions");

            migrationBuilder.DropIndex(
                name: "ix_inscriptions_depuis_inscription_id",
                table: "inscriptions");

            migrationBuilder.DropIndex(
                name: "ix_inscriptions_eleve_id_groupe_id",
                table: "inscriptions");

            migrationBuilder.DropColumn(
                name: "part_du_report",
                table: "reglements");

            migrationBuilder.DropColumn(
                name: "arrete_le",
                table: "inscriptions");

            migrationBuilder.DropColumn(
                name: "depuis_inscription_id",
                table: "inscriptions");

            migrationBuilder.CreateIndex(
                name: "ix_inscriptions_eleve_id_groupe_id",
                table: "inscriptions",
                columns: new[] { "eleve_id", "groupe_id" },
                unique: true);
        }
    }
}
