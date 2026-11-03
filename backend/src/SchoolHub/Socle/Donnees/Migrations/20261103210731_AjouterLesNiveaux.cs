using System;
using Microsoft.EntityFrameworkCore.Migrations;
using SchoolHub.Socle.Centres;

#nullable disable

namespace SchoolHub.Socle.Donnees.Migrations
{
    /// <inheritdoc />
    public partial class AjouterLesNiveaux : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "niveaux",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    centre_id = table.Column<Guid>(type: "uuid", nullable: false),
                    nom = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    nom_normalise = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    d_office = table.Column<bool>(type: "boolean", nullable: false),
                    rang = table.Column<int>(type: "integer", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_niveaux", x => x.id);
                    table.ForeignKey(
                        name: "fk_niveaux_centres_centre_id",
                        column: x => x.centre_id,
                        principalTable: "centres",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "ix_niveaux_centre_id_nom_normalise",
                table: "niveaux",
                columns: new[] { "centre_id", "nom_normalise" },
                unique: true);

            // Les centres qui existaient avant reçoivent les 29 niveaux d'office. Si la liste change un jour,
            // la nouvelle migration recopiera ici le SQL d'aujourd'hui, pour figer cette migration-ci.
            migrationBuilder.Sql(NiveauxDOffice.SqlDeRecopie());
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "niveaux");
        }
    }
}
