using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace SchoolHub.Socle.Donnees.Migrations
{
    /// <inheritdoc />
    public partial class AjouterLesDemandes : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "demandes_d_inscription",
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
                    nom_de_recherche = table.Column<string>(type: "character varying(201)", maxLength: 201, nullable: false),
                    amis = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    recue_le = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    etat = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_demandes_d_inscription", x => x.id);
                    table.ForeignKey(
                        name: "fk_demandes_d_inscription_centres_centre_id",
                        column: x => x.centre_id,
                        principalTable: "centres",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_demandes_d_inscription_niveaux_niveau_id",
                        column: x => x.niveau_id,
                        principalTable: "niveaux",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "groupes_demandes",
                columns: table => new
                {
                    demande_d_inscription_id = table.Column<Guid>(type: "uuid", nullable: false),
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    groupe_id = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_groupes_demandes", x => new { x.demande_d_inscription_id, x.id });
                    table.ForeignKey(
                        name: "fk_groupes_demandes_demandes_d_inscription_demande_d_inscripti",
                        column: x => x.demande_d_inscription_id,
                        principalTable: "demandes_d_inscription",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "fk_groupes_demandes_groupes_groupe_id",
                        column: x => x.groupe_id,
                        principalTable: "groupes",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "ix_demandes_d_inscription_centre_id",
                table: "demandes_d_inscription",
                column: "centre_id");

            migrationBuilder.CreateIndex(
                name: "ix_demandes_d_inscription_etat_recue_le",
                table: "demandes_d_inscription",
                columns: new[] { "etat", "recue_le" });

            migrationBuilder.CreateIndex(
                name: "ix_demandes_d_inscription_niveau_id",
                table: "demandes_d_inscription",
                column: "niveau_id");

            migrationBuilder.CreateIndex(
                name: "ix_groupes_demandes_groupe_id",
                table: "groupes_demandes",
                column: "groupe_id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "groupes_demandes");

            migrationBuilder.DropTable(
                name: "demandes_d_inscription");
        }
    }
}
