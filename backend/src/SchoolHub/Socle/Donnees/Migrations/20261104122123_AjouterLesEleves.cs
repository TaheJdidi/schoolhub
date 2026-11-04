using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SchoolHub.Socle.Donnees.Migrations
{
    /// <inheritdoc />
    public partial class AjouterLesEleves : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "eleves",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    centre_id = table.Column<Guid>(type: "uuid", nullable: false),
                    prenom = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    nom = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    niveau_id = table.Column<Guid>(type: "uuid", nullable: false),
                    majeur = table.Column<bool>(type: "boolean", nullable: false),
                    telephone_parent = table.Column<string>(type: "character varying(8)", maxLength: 8, nullable: true),
                    email_parent = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    telephone_eleve = table.Column<string>(type: "character varying(8)", maxLength: 8, nullable: true),
                    email_eleve = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    etablissement = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    nom_de_recherche = table.Column<string>(type: "character varying(201)", maxLength: 201, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_eleves", x => x.id);
                    table.ForeignKey(
                        name: "fk_eleves_centres_centre_id",
                        column: x => x.centre_id,
                        principalTable: "centres",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_eleves_niveaux_niveau_id",
                        column: x => x.niveau_id,
                        principalTable: "niveaux",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "ix_eleves_centre_id",
                table: "eleves",
                column: "centre_id");

            migrationBuilder.CreateIndex(
                name: "ix_eleves_niveau_id",
                table: "eleves",
                column: "niveau_id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "eleves");
        }
    }
}
