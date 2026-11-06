using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SchoolHub.Socle.Donnees.Migrations
{
    /// <inheritdoc />
    public partial class ChangerLeCalendrier : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "annulee_le",
                table: "seances",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "motif_d_annulation",
                table: "seances",
                type: "character varying(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "nature",
                table: "seances",
                type: "character varying(20)",
                maxLength: 20,
                nullable: false,
                // Les séances déjà enregistrées viennent toutes des créneaux de leur groupe.
                defaultValue: "Prevue");

            migrationBuilder.AddColumn<Guid>(
                name: "remplace_la_seance_id",
                table: "seances",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "ix_seances_remplace_la_seance_id",
                table: "seances",
                column: "remplace_la_seance_id");

            migrationBuilder.AddForeignKey(
                name: "fk_seances_seances_remplace_la_seance_id",
                table: "seances",
                column: "remplace_la_seance_id",
                principalTable: "seances",
                principalColumn: "id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "fk_seances_seances_remplace_la_seance_id",
                table: "seances");

            migrationBuilder.DropIndex(
                name: "ix_seances_remplace_la_seance_id",
                table: "seances");

            migrationBuilder.DropColumn(
                name: "annulee_le",
                table: "seances");

            migrationBuilder.DropColumn(
                name: "motif_d_annulation",
                table: "seances");

            migrationBuilder.DropColumn(
                name: "nature",
                table: "seances");

            migrationBuilder.DropColumn(
                name: "remplace_la_seance_id",
                table: "seances");
        }
    }
}
