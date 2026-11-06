using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SchoolHub.Socle.Donnees.Migrations
{
    /// <inheritdoc />
    public partial class AjouterLesParts : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "avances",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    centre_id = table.Column<Guid>(type: "uuid", nullable: false),
                    professeur_id = table.Column<Guid>(type: "uuid", nullable: false),
                    montant = table.Column<decimal>(type: "numeric(12,3)", precision: 12, scale: 3, nullable: false),
                    le = table.Column<DateOnly>(type: "date", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_avances", x => x.id);
                    table.ForeignKey(
                        name: "fk_avances_centres_centre_id",
                        column: x => x.centre_id,
                        principalTable: "centres",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_avances_professeurs_professeur_id",
                        column: x => x.professeur_id,
                        principalTable: "professeurs",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "parts_des_professeurs",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    centre_id = table.Column<Guid>(type: "uuid", nullable: false),
                    professeur_id = table.Column<Guid>(type: "uuid", nullable: false),
                    pourcentage = table.Column<decimal>(type: "numeric(5,2)", precision: 5, scale: 2, nullable: true),
                    changee_le = table.Column<DateOnly>(type: "date", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_parts_des_professeurs", x => x.id);
                    table.ForeignKey(
                        name: "fk_parts_des_professeurs_centres_centre_id",
                        column: x => x.centre_id,
                        principalTable: "centres",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_parts_des_professeurs_professeurs_professeur_id",
                        column: x => x.professeur_id,
                        principalTable: "professeurs",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "parts_payees",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    centre_id = table.Column<Guid>(type: "uuid", nullable: false),
                    groupe_id = table.Column<Guid>(type: "uuid", nullable: false),
                    cle = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    professeur_id = table.Column<Guid>(type: "uuid", nullable: false),
                    montant_du_groupe = table.Column<decimal>(type: "numeric(12,3)", precision: 12, scale: 3, nullable: false),
                    pourcentage = table.Column<decimal>(type: "numeric(5,2)", precision: 5, scale: 2, nullable: false),
                    part_du_centre = table.Column<decimal>(type: "numeric(12,3)", precision: 12, scale: 3, nullable: false),
                    avance_deduite = table.Column<decimal>(type: "numeric(12,3)", precision: 12, scale: 3, nullable: false),
                    a_verser = table.Column<decimal>(type: "numeric(12,3)", precision: 12, scale: 3, nullable: false),
                    payee_le = table.Column<DateOnly>(type: "date", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_parts_payees", x => x.id);
                    table.ForeignKey(
                        name: "fk_parts_payees_centres_centre_id",
                        column: x => x.centre_id,
                        principalTable: "centres",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_parts_payees_groupes_groupe_id",
                        column: x => x.groupe_id,
                        principalTable: "groupes",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_parts_payees_professeurs_professeur_id",
                        column: x => x.professeur_id,
                        principalTable: "professeurs",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "ix_avances_centre_id",
                table: "avances",
                column: "centre_id");

            migrationBuilder.CreateIndex(
                name: "ix_avances_professeur_id",
                table: "avances",
                column: "professeur_id");

            migrationBuilder.CreateIndex(
                name: "ix_parts_des_professeurs_centre_id",
                table: "parts_des_professeurs",
                column: "centre_id");

            migrationBuilder.CreateIndex(
                name: "ix_parts_des_professeurs_professeur_id",
                table: "parts_des_professeurs",
                column: "professeur_id");

            migrationBuilder.CreateIndex(
                name: "ix_parts_payees_centre_id",
                table: "parts_payees",
                column: "centre_id");

            migrationBuilder.CreateIndex(
                name: "ix_parts_payees_groupe_id_cle",
                table: "parts_payees",
                columns: new[] { "groupe_id", "cle" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_parts_payees_professeur_id",
                table: "parts_payees",
                column: "professeur_id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "avances");

            migrationBuilder.DropTable(
                name: "parts_des_professeurs");

            migrationBuilder.DropTable(
                name: "parts_payees");
        }
    }
}
