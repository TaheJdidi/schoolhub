using System;
using Microsoft.EntityFrameworkCore.Migrations;
using SchoolHub.Modules.Abonnements;

#nullable disable

namespace SchoolHub.Socle.Donnees.Migrations
{
    /// <inheritdoc />
    public partial class BackOfficeDeLEditeur : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<Guid>(
                name: "centre_id",
                table: "comptes",
                type: "uuid",
                nullable: true,
                oldClrType: typeof(Guid),
                oldType: "uuid");

            migrationBuilder.CreateTable(
                name: "abonnements",
                columns: table => new
                {
                    centre_id = table.Column<Guid>(type: "uuid", nullable: false),
                    prix_mensuel = table.Column<decimal>(type: "numeric(12,3)", precision: 12, scale: 3, nullable: false),
                    paye_jusqu_au = table.Column<DateOnly>(type: "date", nullable: true),
                    forcage = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    motif_du_forcage = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: true),
                    force_le = table.Column<DateOnly>(type: "date", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_abonnements", x => x.centre_id);
                    table.ForeignKey(
                        name: "fk_abonnements_centres_centre_id",
                        column: x => x.centre_id,
                        principalTable: "centres",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "journal_editeur",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    quand = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    auteur_id = table.Column<Guid>(type: "uuid", nullable: true),
                    auteur = table.Column<string>(type: "character varying(201)", maxLength: 201, nullable: false),
                    action = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    centre_id = table.Column<Guid>(type: "uuid", nullable: false),
                    centre = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    detail = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_journal_editeur", x => x.id);
                    table.ForeignKey(
                        name: "fk_journal_editeur_asp_net_users_auteur_id",
                        column: x => x.auteur_id,
                        principalTable: "comptes",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "paiements_d_abonnement",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    centre_id = table.Column<Guid>(type: "uuid", nullable: false),
                    date = table.Column<DateOnly>(type: "date", nullable: false),
                    montant = table.Column<decimal>(type: "numeric(12,3)", precision: 12, scale: 3, nullable: false),
                    mode = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    du = table.Column<DateOnly>(type: "date", nullable: false),
                    au = table.Column<DateOnly>(type: "date", nullable: false),
                    numero_de_facture = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    saisi_le = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    saisi_par = table.Column<string>(type: "character varying(201)", maxLength: 201, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_paiements_d_abonnement", x => x.id);
                    table.ForeignKey(
                        name: "fk_paiements_d_abonnement_centres_centre_id",
                        column: x => x.centre_id,
                        principalTable: "centres",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "envois_au_proprietaire",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    centre_id = table.Column<Guid>(type: "uuid", nullable: false),
                    objet = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    canal = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    quand = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    destinataire = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    paiement_id = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_envois_au_proprietaire", x => x.id);
                    table.ForeignKey(
                        name: "fk_envois_au_proprietaire_centres_centre_id",
                        column: x => x.centre_id,
                        principalTable: "centres",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_envois_au_proprietaire_paiement_d_abonnement_paiement_id",
                        column: x => x.paiement_id,
                        principalTable: "paiements_d_abonnement",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.AddCheckConstraint(
                name: "ck_comptes_centre_sauf_editeur",
                table: "comptes",
                sql: "(role = 'Editeur') = (centre_id IS NULL)");

            migrationBuilder.CreateIndex(
                name: "ix_envois_au_proprietaire_centre_id",
                table: "envois_au_proprietaire",
                column: "centre_id");

            migrationBuilder.CreateIndex(
                name: "ix_envois_au_proprietaire_paiement_id",
                table: "envois_au_proprietaire",
                column: "paiement_id");

            migrationBuilder.CreateIndex(
                name: "ix_journal_editeur_auteur_id",
                table: "journal_editeur",
                column: "auteur_id");

            migrationBuilder.CreateIndex(
                name: "ix_paiements_d_abonnement_centre_id",
                table: "paiements_d_abonnement",
                column: "centre_id");
            // Le journal de l'Éditeur ne se modifie ni ne s'efface (EDI-05).
            migrationBuilder.Sql(JournalDeLEditeur.SqlDeLaProtection);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(JournalDeLEditeur.SqlDuRetour);

            migrationBuilder.DropTable(
                name: "abonnements");

            migrationBuilder.DropTable(
                name: "envois_au_proprietaire");

            migrationBuilder.DropTable(
                name: "journal_editeur");

            migrationBuilder.DropTable(
                name: "paiements_d_abonnement");

            migrationBuilder.DropCheckConstraint(
                name: "ck_comptes_centre_sauf_editeur",
                table: "comptes");

            migrationBuilder.AlterColumn<Guid>(
                name: "centre_id",
                table: "comptes",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"),
                oldClrType: typeof(Guid),
                oldType: "uuid",
                oldNullable: true);
        }
    }
}
