using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SchoolHub.Socle.Donnees.Migrations
{
    /// <inheritdoc />
    public partial class AjouterLesReglements : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<decimal>(
                name: "prix_special",
                table: "inscriptions",
                type: "numeric(12,3)",
                precision: 12,
                scale: 3,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "raison_du_prix_special",
                table: "inscriptions",
                type: "character varying(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.AddColumn<DateOnly>(
                name: "reste_regroupe_le",
                table: "inscriptions",
                type: "date",
                nullable: true);

            migrationBuilder.AddColumn<DateOnly>(
                name: "dispensee_le",
                table: "assurances",
                type: "date",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "raison_de_la_dispense",
                table: "assurances",
                type: "character varying(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.CreateTable(
                name: "reglements",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    centre_id = table.Column<Guid>(type: "uuid", nullable: false),
                    inscription_id = table.Column<Guid>(type: "uuid", nullable: true),
                    assurance_id = table.Column<Guid>(type: "uuid", nullable: true),
                    cle = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    montant = table.Column<decimal>(type: "numeric(12,3)", precision: 12, scale: 3, nullable: false),
                    origine = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    regle_le = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    auteur_id = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_reglements", x => x.id);
                    table.CheckConstraint("ck_reglements_une_somme", "(inscription_id IS NULL) <> (assurance_id IS NULL)");
                    table.ForeignKey(
                        name: "fk_reglements_asp_net_users_auteur_id",
                        column: x => x.auteur_id,
                        principalTable: "comptes",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_reglements_assurances_assurance_id",
                        column: x => x.assurance_id,
                        principalTable: "assurances",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_reglements_centres_centre_id",
                        column: x => x.centre_id,
                        principalTable: "centres",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_reglements_inscriptions_inscription_id",
                        column: x => x.inscription_id,
                        principalTable: "inscriptions",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "ix_reglements_assurance_id",
                table: "reglements",
                column: "assurance_id",
                unique: true,
                filter: "assurance_id IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "ix_reglements_auteur_id",
                table: "reglements",
                column: "auteur_id");

            migrationBuilder.CreateIndex(
                name: "ix_reglements_centre_id",
                table: "reglements",
                column: "centre_id");

            migrationBuilder.CreateIndex(
                name: "ix_reglements_inscription_id_cle",
                table: "reglements",
                columns: new[] { "inscription_id", "cle" },
                unique: true,
                filter: "inscription_id IS NOT NULL");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "reglements");

            migrationBuilder.DropColumn(
                name: "prix_special",
                table: "inscriptions");

            migrationBuilder.DropColumn(
                name: "raison_du_prix_special",
                table: "inscriptions");

            migrationBuilder.DropColumn(
                name: "reste_regroupe_le",
                table: "inscriptions");

            migrationBuilder.DropColumn(
                name: "dispensee_le",
                table: "assurances");

            migrationBuilder.DropColumn(
                name: "raison_de_la_dispense",
                table: "assurances");
        }
    }
}
