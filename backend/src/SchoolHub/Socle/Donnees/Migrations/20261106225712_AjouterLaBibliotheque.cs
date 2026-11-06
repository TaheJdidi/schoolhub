using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SchoolHub.Socle.Donnees.Migrations
{
    /// <inheritdoc />
    public partial class AjouterLaBibliotheque : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "entrees_a_la_bibliotheque",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    centre_id = table.Column<Guid>(type: "uuid", nullable: false),
                    eleve_id = table.Column<Guid>(type: "uuid", nullable: false),
                    jour = table.Column<DateOnly>(type: "date", nullable: false),
                    arrivee_le = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    depart = table.Column<TimeOnly>(type: "time without time zone", nullable: true),
                    recu_id = table.Column<Guid>(type: "uuid", nullable: false),
                    montant = table.Column<decimal>(type: "numeric(12,3)", precision: 12, scale: 3, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_entrees_a_la_bibliotheque", x => x.id);
                    table.ForeignKey(
                        name: "fk_entrees_a_la_bibliotheque_centres_centre_id",
                        column: x => x.centre_id,
                        principalTable: "centres",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_entrees_a_la_bibliotheque_eleves_eleve_id",
                        column: x => x.eleve_id,
                        principalTable: "eleves",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_entrees_a_la_bibliotheque_recus_recu_id",
                        column: x => x.recu_id,
                        principalTable: "recus",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "ix_entrees_a_la_bibliotheque_centre_id",
                table: "entrees_a_la_bibliotheque",
                column: "centre_id");

            migrationBuilder.CreateIndex(
                name: "ix_entrees_a_la_bibliotheque_eleve_id",
                table: "entrees_a_la_bibliotheque",
                column: "eleve_id");

            migrationBuilder.CreateIndex(
                name: "ix_entrees_a_la_bibliotheque_jour_eleve_id",
                table: "entrees_a_la_bibliotheque",
                columns: new[] { "jour", "eleve_id" });

            migrationBuilder.CreateIndex(
                name: "ix_entrees_a_la_bibliotheque_recu_id",
                table: "entrees_a_la_bibliotheque",
                column: "recu_id",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "entrees_a_la_bibliotheque");
        }
    }
}
