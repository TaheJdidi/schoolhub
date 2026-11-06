using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SchoolHub.Socle.Donnees.Migrations
{
    /// <inheritdoc />
    public partial class AjouterLesAchats : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "achats",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    centre_id = table.Column<Guid>(type: "uuid", nullable: false),
                    jour = table.Column<DateOnly>(type: "date", nullable: false),
                    libelle = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    categorie = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    montant = table.Column<decimal>(type: "numeric(12,3)", precision: 12, scale: 3, nullable: false),
                    fournisseur = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    paye_depuis_la_caisse = table.Column<bool>(type: "boolean", nullable: false),
                    auteur_id = table.Column<Guid>(type: "uuid", nullable: false),
                    auteur = table.Column<string>(type: "character varying(201)", maxLength: 201, nullable: false),
                    cree_le = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    cloture_id = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_achats", x => x.id);
                    table.ForeignKey(
                        name: "fk_achats_asp_net_users_auteur_id",
                        column: x => x.auteur_id,
                        principalTable: "comptes",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_achats_centres_centre_id",
                        column: x => x.centre_id,
                        principalTable: "centres",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_achats_clotures_de_caisse_cloture_id",
                        column: x => x.cloture_id,
                        principalTable: "clotures_de_caisse",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "factures_d_achat",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    centre_id = table.Column<Guid>(type: "uuid", nullable: false),
                    achat_id = table.Column<Guid>(type: "uuid", nullable: false),
                    nom = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    type = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    contenu = table.Column<byte[]>(type: "bytea", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_factures_d_achat", x => x.id);
                    table.ForeignKey(
                        name: "fk_factures_d_achat_achats_achat_id",
                        column: x => x.achat_id,
                        principalTable: "achats",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "fk_factures_d_achat_centres_centre_id",
                        column: x => x.centre_id,
                        principalTable: "centres",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "ix_achats_auteur_id_cloture_id",
                table: "achats",
                columns: new[] { "auteur_id", "cloture_id" });

            migrationBuilder.CreateIndex(
                name: "ix_achats_centre_id",
                table: "achats",
                column: "centre_id");

            migrationBuilder.CreateIndex(
                name: "ix_achats_cloture_id",
                table: "achats",
                column: "cloture_id");

            migrationBuilder.CreateIndex(
                name: "ix_achats_jour",
                table: "achats",
                column: "jour");

            migrationBuilder.CreateIndex(
                name: "ix_factures_d_achat_achat_id",
                table: "factures_d_achat",
                column: "achat_id",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_factures_d_achat_centre_id",
                table: "factures_d_achat",
                column: "centre_id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "factures_d_achat");

            migrationBuilder.DropTable(
                name: "achats");
        }
    }
}
