using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SchoolHub.Socle.Donnees.Migrations
{
    /// <inheritdoc />
    public partial class TraiterLesDemandes : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "eleve_id",
                table: "demandes_d_inscription",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "motif",
                table: "demandes_d_inscription",
                type: "character varying(20)",
                maxLength: 20,
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "traitee_le",
                table: "demandes_d_inscription",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "ix_demandes_d_inscription_eleve_id",
                table: "demandes_d_inscription",
                column: "eleve_id");

            migrationBuilder.AddForeignKey(
                name: "fk_demandes_d_inscription_eleves_eleve_id",
                table: "demandes_d_inscription",
                column: "eleve_id",
                principalTable: "eleves",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "fk_demandes_d_inscription_eleves_eleve_id",
                table: "demandes_d_inscription");

            migrationBuilder.DropIndex(
                name: "ix_demandes_d_inscription_eleve_id",
                table: "demandes_d_inscription");

            migrationBuilder.DropColumn(
                name: "eleve_id",
                table: "demandes_d_inscription");

            migrationBuilder.DropColumn(
                name: "motif",
                table: "demandes_d_inscription");

            migrationBuilder.DropColumn(
                name: "traitee_le",
                table: "demandes_d_inscription");
        }
    }
}
