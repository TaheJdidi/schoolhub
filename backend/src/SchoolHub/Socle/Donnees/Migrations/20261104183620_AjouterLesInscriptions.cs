using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SchoolHub.Socle.Donnees.Migrations
{
    /// <inheritdoc />
    public partial class AjouterLesInscriptions : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "assurances",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    centre_id = table.Column<Guid>(type: "uuid", nullable: false),
                    eleve_id = table.Column<Guid>(type: "uuid", nullable: false),
                    annee_scolaire_id = table.Column<Guid>(type: "uuid", nullable: false),
                    montant = table.Column<decimal>(type: "numeric(12,3)", precision: 12, scale: 3, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_assurances", x => x.id);
                    table.ForeignKey(
                        name: "fk_assurances_annees_scolaires_annee_scolaire_id",
                        column: x => x.annee_scolaire_id,
                        principalTable: "annees_scolaires",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_assurances_centres_centre_id",
                        column: x => x.centre_id,
                        principalTable: "centres",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_assurances_eleves_eleve_id",
                        column: x => x.eleve_id,
                        principalTable: "eleves",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "inscriptions",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    centre_id = table.Column<Guid>(type: "uuid", nullable: false),
                    eleve_id = table.Column<Guid>(type: "uuid", nullable: false),
                    groupe_id = table.Column<Guid>(type: "uuid", nullable: false),
                    mode = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    debut_le = table.Column<DateOnly>(type: "date", nullable: false),
                    inscrit_le = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_inscriptions", x => x.id);
                    table.ForeignKey(
                        name: "fk_inscriptions_centres_centre_id",
                        column: x => x.centre_id,
                        principalTable: "centres",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_inscriptions_eleves_eleve_id",
                        column: x => x.eleve_id,
                        principalTable: "eleves",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_inscriptions_groupes_groupe_id",
                        column: x => x.groupe_id,
                        principalTable: "groupes",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "ix_assurances_annee_scolaire_id",
                table: "assurances",
                column: "annee_scolaire_id");

            migrationBuilder.CreateIndex(
                name: "ix_assurances_centre_id",
                table: "assurances",
                column: "centre_id");

            migrationBuilder.CreateIndex(
                name: "ix_assurances_eleve_id_annee_scolaire_id",
                table: "assurances",
                columns: new[] { "eleve_id", "annee_scolaire_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_inscriptions_centre_id",
                table: "inscriptions",
                column: "centre_id");

            migrationBuilder.CreateIndex(
                name: "ix_inscriptions_eleve_id_groupe_id",
                table: "inscriptions",
                columns: new[] { "eleve_id", "groupe_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_inscriptions_groupe_id",
                table: "inscriptions",
                column: "groupe_id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "assurances");

            migrationBuilder.DropTable(
                name: "inscriptions");
        }
    }
}
