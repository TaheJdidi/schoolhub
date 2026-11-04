using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SchoolHub.Socle.Donnees.Migrations
{
    /// <inheritdoc />
    public partial class AjouterLaFermetureDesGroupes : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateOnly>(
                name: "ferme_le",
                table: "groupes",
                type: "date",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "motif_de_fermeture",
                table: "groupes",
                type: "character varying(200)",
                maxLength: 200,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ferme_le",
                table: "groupes");

            migrationBuilder.DropColumn(
                name: "motif_de_fermeture",
                table: "groupes");
        }
    }
}
