using System;
using Microsoft.EntityFrameworkCore.Migrations;
using SchoolHub.Socle.Journal;

#nullable disable

namespace SchoolHub.Socle.Donnees.Migrations
{
    /// <inheritdoc />
    public partial class AjouterLeJournal : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "journal",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    centre_id = table.Column<Guid>(type: "uuid", nullable: false),
                    quand = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    auteur_id = table.Column<Guid>(type: "uuid", nullable: true),
                    auteur = table.Column<string>(type: "character varying(201)", maxLength: 201, nullable: false),
                    action = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    detail = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    avant = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    apres = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_journal", x => x.id);
                    table.ForeignKey(
                        name: "fk_journal_centres_centre_id",
                        column: x => x.centre_id,
                        principalTable: "centres",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_journal_comptes_auteur_id",
                        column: x => x.auteur_id,
                        principalTable: "comptes",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "ix_journal_auteur_id",
                table: "journal",
                column: "auteur_id");

            migrationBuilder.CreateIndex(
                name: "ix_journal_centre_id",
                table: "journal",
                column: "centre_id");

            // Le trigger qui refuse de modifier ou d'effacer une ligne. Si la protection change un jour,
            // la nouvelle migration recopiera ici le SQL d'aujourd'hui, pour figer cette migration-ci.
            migrationBuilder.Sql(JournalDuCentre.SqlDeLaProtection);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "journal");

            // Les triggers partent avec la table ; leur fonction reste, à supprimer à part.
            migrationBuilder.Sql("DROP FUNCTION journal_refuser_les_changements();");
        }
    }
}
