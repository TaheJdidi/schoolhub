using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace SchoolHub.Socle.Donnees.Migrations
{
    /// <inheritdoc />
    public partial class AjouterLesGroupes : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "matieres",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    centre_id = table.Column<Guid>(type: "uuid", nullable: false),
                    nom = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    nom_normalise = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_matieres", x => x.id);
                    table.ForeignKey(
                        name: "fk_matieres_centres_centre_id",
                        column: x => x.centre_id,
                        principalTable: "centres",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "groupes",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    centre_id = table.Column<Guid>(type: "uuid", nullable: false),
                    type = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    nom = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    professeur_id = table.Column<Guid>(type: "uuid", nullable: false),
                    matiere_id = table.Column<Guid>(type: "uuid", nullable: false),
                    niveau_id = table.Column<Guid>(type: "uuid", nullable: true),
                    annee_scolaire_id = table.Column<Guid>(type: "uuid", nullable: false),
                    capacite = table.Column<int>(type: "integer", nullable: true),
                    date_de_debut = table.Column<DateOnly>(type: "date", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_groupes", x => x.id);
                    table.ForeignKey(
                        name: "fk_groupes_annees_scolaires_annee_scolaire_id",
                        column: x => x.annee_scolaire_id,
                        principalTable: "annees_scolaires",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_groupes_centres_centre_id",
                        column: x => x.centre_id,
                        principalTable: "centres",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_groupes_matieres_matiere_id",
                        column: x => x.matiere_id,
                        principalTable: "matieres",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_groupes_niveaux_niveau_id",
                        column: x => x.niveau_id,
                        principalTable: "niveaux",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_groupes_professeurs_professeur_id",
                        column: x => x.professeur_id,
                        principalTable: "professeurs",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "creneaux_des_groupes",
                columns: table => new
                {
                    groupe_id = table.Column<Guid>(type: "uuid", nullable: false),
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    jour = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    debut = table.Column<TimeOnly>(type: "time without time zone", nullable: false),
                    fin = table.Column<TimeOnly>(type: "time without time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_creneaux_des_groupes", x => new { x.groupe_id, x.id });
                    table.ForeignKey(
                        name: "fk_creneaux_des_groupes_groupes_groupe_id",
                        column: x => x.groupe_id,
                        principalTable: "groupes",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "prix_des_groupes",
                columns: table => new
                {
                    groupe_id = table.Column<Guid>(type: "uuid", nullable: false),
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    a_partir_du_mois = table.Column<int>(type: "integer", nullable: false),
                    montant = table.Column<decimal>(type: "numeric(12,3)", precision: 12, scale: 3, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_prix_des_groupes", x => new { x.groupe_id, x.id });
                    table.ForeignKey(
                        name: "fk_prix_des_groupes_groupes_groupe_id",
                        column: x => x.groupe_id,
                        principalTable: "groupes",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "seances",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    centre_id = table.Column<Guid>(type: "uuid", nullable: false),
                    groupe_id = table.Column<Guid>(type: "uuid", nullable: false),
                    date = table.Column<DateOnly>(type: "date", nullable: false),
                    debut = table.Column<TimeOnly>(type: "time without time zone", nullable: false),
                    fin = table.Column<TimeOnly>(type: "time without time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_seances", x => x.id);
                    table.ForeignKey(
                        name: "fk_seances_centres_centre_id",
                        column: x => x.centre_id,
                        principalTable: "centres",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_seances_groupes_groupe_id",
                        column: x => x.groupe_id,
                        principalTable: "groupes",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "ix_groupes_annee_scolaire_id",
                table: "groupes",
                column: "annee_scolaire_id");

            migrationBuilder.CreateIndex(
                name: "ix_groupes_centre_id",
                table: "groupes",
                column: "centre_id");

            migrationBuilder.CreateIndex(
                name: "ix_groupes_matiere_id",
                table: "groupes",
                column: "matiere_id");

            migrationBuilder.CreateIndex(
                name: "ix_groupes_niveau_id",
                table: "groupes",
                column: "niveau_id");

            migrationBuilder.CreateIndex(
                name: "ix_groupes_professeur_id",
                table: "groupes",
                column: "professeur_id");

            migrationBuilder.CreateIndex(
                name: "ix_matieres_centre_id_nom_normalise",
                table: "matieres",
                columns: new[] { "centre_id", "nom_normalise" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_seances_centre_id",
                table: "seances",
                column: "centre_id");

            migrationBuilder.CreateIndex(
                name: "ix_seances_groupe_id_date",
                table: "seances",
                columns: new[] { "groupe_id", "date" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "creneaux_des_groupes");

            migrationBuilder.DropTable(
                name: "prix_des_groupes");

            migrationBuilder.DropTable(
                name: "seances");

            migrationBuilder.DropTable(
                name: "groupes");

            migrationBuilder.DropTable(
                name: "matieres");
        }
    }
}
