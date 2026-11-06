using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SchoolHub.Socle.Donnees.Migrations
{
    /// <inheritdoc />
    public partial class AjouterLesRessourcesHumaines : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "employes",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    centre_id = table.Column<Guid>(type: "uuid", nullable: false),
                    prenom = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    nom = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    email = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    telephone = table.Column<string>(type: "character varying(8)", maxLength: 8, nullable: true),
                    poste = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    horaires = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: true),
                    salaire = table.Column<decimal>(type: "numeric(12,3)", precision: 12, scale: 3, nullable: false),
                    parti_le = table.Column<DateOnly>(type: "date", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_employes", x => x.id);
                    table.ForeignKey(
                        name: "fk_employes_centres_centre_id",
                        column: x => x.centre_id,
                        principalTable: "centres",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "conges",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    centre_id = table.Column<Guid>(type: "uuid", nullable: false),
                    employe_id = table.Column<Guid>(type: "uuid", nullable: false),
                    du = table.Column<DateOnly>(type: "date", nullable: false),
                    au = table.Column<DateOnly>(type: "date", nullable: false),
                    note = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_conges", x => x.id);
                    table.ForeignKey(
                        name: "fk_conges_centres_centre_id",
                        column: x => x.centre_id,
                        principalTable: "centres",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_conges_employes_employe_id",
                        column: x => x.employe_id,
                        principalTable: "employes",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "salaires_payes",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    centre_id = table.Column<Guid>(type: "uuid", nullable: false),
                    employe_id = table.Column<Guid>(type: "uuid", nullable: false),
                    mois = table.Column<DateOnly>(type: "date", nullable: false),
                    montant = table.Column<decimal>(type: "numeric(12,3)", precision: 12, scale: 3, nullable: false),
                    paye_le = table.Column<DateOnly>(type: "date", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_salaires_payes", x => x.id);
                    table.ForeignKey(
                        name: "fk_salaires_payes_centres_centre_id",
                        column: x => x.centre_id,
                        principalTable: "centres",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_salaires_payes_employes_employe_id",
                        column: x => x.employe_id,
                        principalTable: "employes",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "ix_conges_centre_id",
                table: "conges",
                column: "centre_id");

            migrationBuilder.CreateIndex(
                name: "ix_conges_employe_id",
                table: "conges",
                column: "employe_id");

            migrationBuilder.CreateIndex(
                name: "ix_employes_centre_id",
                table: "employes",
                column: "centre_id");

            migrationBuilder.CreateIndex(
                name: "ix_salaires_payes_centre_id",
                table: "salaires_payes",
                column: "centre_id");

            migrationBuilder.CreateIndex(
                name: "ix_salaires_payes_employe_id_mois",
                table: "salaires_payes",
                columns: new[] { "employe_id", "mois" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "conges");

            migrationBuilder.DropTable(
                name: "salaires_payes");

            migrationBuilder.DropTable(
                name: "employes");
        }
    }
}
