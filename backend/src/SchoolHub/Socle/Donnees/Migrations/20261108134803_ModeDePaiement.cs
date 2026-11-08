using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SchoolHub.Socle.Donnees.Migrations
{
    /// <inheritdoc />
    public partial class ModeDePaiement : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "mode",
                table: "recus",
                type: "character varying(10)",
                maxLength: 10,
                nullable: false,
                // Les reçus d'avant ce champ étaient tous en espèces.
                defaultValue: "Especes");

            migrationBuilder.AddColumn<decimal>(
                name: "cheques",
                table: "clotures_de_caisse",
                type: "numeric(12,3)",
                precision: 12,
                scale: 3,
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<int>(
                name: "nombre_de_cheques",
                table: "clotures_de_caisse",
                type: "integer",
                nullable: false,
                defaultValue: 0);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "mode",
                table: "recus");

            migrationBuilder.DropColumn(
                name: "cheques",
                table: "clotures_de_caisse");

            migrationBuilder.DropColumn(
                name: "nombre_de_cheques",
                table: "clotures_de_caisse");
        }
    }
}
