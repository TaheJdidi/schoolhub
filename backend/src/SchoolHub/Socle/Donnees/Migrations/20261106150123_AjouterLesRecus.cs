using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace SchoolHub.Socle.Donnees.Migrations
{
    /// <inheritdoc />
    public partial class AjouterLesRecus : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "recu_id",
                table: "reglements",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "recus",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    centre_id = table.Column<Guid>(type: "uuid", nullable: false),
                    numero = table.Column<int>(type: "integer", nullable: false),
                    eleve_id = table.Column<Guid>(type: "uuid", nullable: false),
                    auteur_id = table.Column<Guid>(type: "uuid", nullable: false),
                    auteur = table.Column<string>(type: "character varying(201)", maxLength: 201, nullable: false),
                    cree_le = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    jour = table.Column<DateOnly>(type: "date", nullable: false),
                    total = table.Column<decimal>(type: "numeric(12,3)", precision: 12, scale: 3, nullable: false),
                    annule_le = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    motif_d_annulation = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    annule_par_id = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_recus", x => x.id);
                    table.ForeignKey(
                        name: "fk_recus_asp_net_users_annule_par_id",
                        column: x => x.annule_par_id,
                        principalTable: "comptes",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_recus_asp_net_users_auteur_id",
                        column: x => x.auteur_id,
                        principalTable: "comptes",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_recus_centres_centre_id",
                        column: x => x.centre_id,
                        principalTable: "centres",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_recus_eleves_eleve_id",
                        column: x => x.eleve_id,
                        principalTable: "eleves",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "lignes_des_recus",
                columns: table => new
                {
                    recu_id = table.Column<Guid>(type: "uuid", nullable: false),
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    groupe = table.Column<string>(type: "character varying(250)", maxLength: 250, nullable: false),
                    libelle = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    montant = table.Column<decimal>(type: "numeric(12,3)", precision: 12, scale: 3, nullable: false),
                    inscription_id = table.Column<Guid>(type: "uuid", nullable: true),
                    assurance_id = table.Column<Guid>(type: "uuid", nullable: true),
                    cle = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_lignes_des_recus", x => new { x.recu_id, x.id });
                    table.ForeignKey(
                        name: "fk_lignes_des_recus_recus_recu_id",
                        column: x => x.recu_id,
                        principalTable: "recus",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "ix_reglements_recu_id",
                table: "reglements",
                column: "recu_id");

            migrationBuilder.CreateIndex(
                name: "ix_recus_annule_par_id",
                table: "recus",
                column: "annule_par_id");

            migrationBuilder.CreateIndex(
                name: "ix_recus_auteur_id",
                table: "recus",
                column: "auteur_id");

            migrationBuilder.CreateIndex(
                name: "ix_recus_centre_id_numero",
                table: "recus",
                columns: new[] { "centre_id", "numero" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_recus_eleve_id",
                table: "recus",
                column: "eleve_id");

            migrationBuilder.CreateIndex(
                name: "ix_recus_jour_auteur_id",
                table: "recus",
                columns: new[] { "jour", "auteur_id" });

            migrationBuilder.AddForeignKey(
                name: "fk_reglements_recus_recu_id",
                table: "reglements",
                column: "recu_id",
                principalTable: "recus",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "fk_reglements_recus_recu_id",
                table: "reglements");

            migrationBuilder.DropTable(
                name: "lignes_des_recus");

            migrationBuilder.DropTable(
                name: "recus");

            migrationBuilder.DropIndex(
                name: "ix_reglements_recu_id",
                table: "reglements");

            migrationBuilder.DropColumn(
                name: "recu_id",
                table: "reglements");
        }
    }
}
