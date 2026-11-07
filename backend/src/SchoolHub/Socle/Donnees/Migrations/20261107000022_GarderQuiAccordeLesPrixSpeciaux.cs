using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SchoolHub.Socle.Donnees.Migrations
{
    /// <inheritdoc />
    public partial class GarderQuiAccordeLesPrixSpeciaux : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateOnly>(
                name: "prix_special_accorde_le",
                table: "inscriptions",
                type: "date",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "prix_special_accorde_par",
                table: "inscriptions",
                type: "character varying(201)",
                maxLength: 201,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "dispensee_par",
                table: "assurances",
                type: "character varying(201)",
                maxLength: 201,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "prix_special_accorde_le",
                table: "inscriptions");

            migrationBuilder.DropColumn(
                name: "prix_special_accorde_par",
                table: "inscriptions");

            migrationBuilder.DropColumn(
                name: "dispensee_par",
                table: "assurances");
        }
    }
}
