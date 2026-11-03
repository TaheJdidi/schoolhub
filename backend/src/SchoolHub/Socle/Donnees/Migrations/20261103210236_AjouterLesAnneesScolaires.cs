using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SchoolHub.Socle.Donnees.Migrations
{
    /// <inheritdoc />
    public partial class AjouterLesAnneesScolaires : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "annees_scolaires",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    centre_id = table.Column<Guid>(type: "uuid", nullable: false),
                    debut = table.Column<DateOnly>(type: "date", nullable: false),
                    fin = table.Column<DateOnly>(type: "date", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_annees_scolaires", x => x.id);
                    table.ForeignKey(
                        name: "fk_annees_scolaires_centres_centre_id",
                        column: x => x.centre_id,
                        principalTable: "centres",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "ix_annees_scolaires_centre_id",
                table: "annees_scolaires",
                column: "centre_id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "annees_scolaires");
        }
    }
}
