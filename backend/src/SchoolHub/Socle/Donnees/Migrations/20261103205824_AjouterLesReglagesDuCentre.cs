using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SchoolHub.Socle.Donnees.Migrations
{
    /// <inheritdoc />
    public partial class AjouterLesReglagesDuCentre : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "capacite_de_la_bibliotheque",
                table: "centres",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "email",
                table: "centres",
                type: "character varying(256)",
                maxLength: 256,
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "montant_de_l_assurance",
                table: "centres",
                type: "numeric(12,3)",
                precision: 12,
                scale: 3,
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "part_du_centre_par_defaut",
                table: "centres",
                type: "numeric(5,2)",
                precision: 5,
                scale: 2,
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "prix_de_l_entree_a_la_bibliotheque",
                table: "centres",
                type: "numeric(12,3)",
                precision: 12,
                scale: 3,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "telephone",
                table: "centres",
                type: "character varying(8)",
                maxLength: 8,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "capacite_de_la_bibliotheque",
                table: "centres");

            migrationBuilder.DropColumn(
                name: "email",
                table: "centres");

            migrationBuilder.DropColumn(
                name: "montant_de_l_assurance",
                table: "centres");

            migrationBuilder.DropColumn(
                name: "part_du_centre_par_defaut",
                table: "centres");

            migrationBuilder.DropColumn(
                name: "prix_de_l_entree_a_la_bibliotheque",
                table: "centres");

            migrationBuilder.DropColumn(
                name: "telephone",
                table: "centres");
        }
    }
}
