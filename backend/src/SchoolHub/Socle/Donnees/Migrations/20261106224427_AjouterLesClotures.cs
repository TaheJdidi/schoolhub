using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SchoolHub.Socle.Donnees.Migrations
{
    /// <inheritdoc />
    public partial class AjouterLesClotures : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "ix_recus_auteur_id",
                table: "recus");

            migrationBuilder.AddColumn<Guid>(
                name: "cloture_id",
                table: "recus",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "clotures_de_caisse",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    centre_id = table.Column<Guid>(type: "uuid", nullable: false),
                    compte_id = table.Column<Guid>(type: "uuid", nullable: false),
                    secretaire = table.Column<string>(type: "character varying(201)", maxLength: 201, nullable: false),
                    jour = table.Column<DateOnly>(type: "date", nullable: false),
                    rang = table.Column<int>(type: "integer", nullable: false),
                    encaisse = table.Column<decimal>(type: "numeric(12,3)", precision: 12, scale: 3, nullable: false),
                    sorties = table.Column<decimal>(type: "numeric(12,3)", precision: 12, scale: 3, nullable: false),
                    attendu = table.Column<decimal>(type: "numeric(12,3)", precision: 12, scale: 3, nullable: false),
                    compte = table.Column<decimal>(type: "numeric(12,3)", precision: 12, scale: 3, nullable: false),
                    ecart = table.Column<decimal>(type: "numeric(12,3)", precision: 12, scale: 3, nullable: false),
                    cloturee_le = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    argent_recu_le = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    argent_recu_par_id = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_clotures_de_caisse", x => x.id);
                    table.ForeignKey(
                        name: "fk_clotures_de_caisse_asp_net_users_argent_recu_par_id",
                        column: x => x.argent_recu_par_id,
                        principalTable: "comptes",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_clotures_de_caisse_asp_net_users_compte_id",
                        column: x => x.compte_id,
                        principalTable: "comptes",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_clotures_de_caisse_centres_centre_id",
                        column: x => x.centre_id,
                        principalTable: "centres",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "ix_recus_auteur_id_cloture_id",
                table: "recus",
                columns: new[] { "auteur_id", "cloture_id" });

            migrationBuilder.CreateIndex(
                name: "ix_recus_cloture_id",
                table: "recus",
                column: "cloture_id");

            migrationBuilder.CreateIndex(
                name: "ix_clotures_de_caisse_argent_recu_par_id",
                table: "clotures_de_caisse",
                column: "argent_recu_par_id");

            migrationBuilder.CreateIndex(
                name: "ix_clotures_de_caisse_centre_id",
                table: "clotures_de_caisse",
                column: "centre_id");

            migrationBuilder.CreateIndex(
                name: "ix_clotures_de_caisse_compte_id_jour_rang",
                table: "clotures_de_caisse",
                columns: new[] { "compte_id", "jour", "rang" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_clotures_de_caisse_jour",
                table: "clotures_de_caisse",
                column: "jour");

            migrationBuilder.AddForeignKey(
                name: "fk_recus_clotures_de_caisse_cloture_id",
                table: "recus",
                column: "cloture_id",
                principalTable: "clotures_de_caisse",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "fk_recus_clotures_de_caisse_cloture_id",
                table: "recus");

            migrationBuilder.DropTable(
                name: "clotures_de_caisse");

            migrationBuilder.DropIndex(
                name: "ix_recus_auteur_id_cloture_id",
                table: "recus");

            migrationBuilder.DropIndex(
                name: "ix_recus_cloture_id",
                table: "recus");

            migrationBuilder.DropColumn(
                name: "cloture_id",
                table: "recus");

            migrationBuilder.CreateIndex(
                name: "ix_recus_auteur_id",
                table: "recus",
                column: "auteur_id");
        }
    }
}
