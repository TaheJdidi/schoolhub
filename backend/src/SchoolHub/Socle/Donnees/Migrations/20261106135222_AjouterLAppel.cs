using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SchoolHub.Socle.Donnees.Migrations
{
    /// <inheritdoc />
    public partial class AjouterLAppel : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "appel_fait_le",
                table: "seances",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "absences",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    centre_id = table.Column<Guid>(type: "uuid", nullable: false),
                    seance_id = table.Column<Guid>(type: "uuid", nullable: false),
                    inscription_id = table.Column<Guid>(type: "uuid", nullable: false),
                    prevenu_le = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_absences", x => x.id);
                    table.ForeignKey(
                        name: "fk_absences_centres_centre_id",
                        column: x => x.centre_id,
                        principalTable: "centres",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_absences_inscriptions_inscription_id",
                        column: x => x.inscription_id,
                        principalTable: "inscriptions",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_absences_seances_seance_id",
                        column: x => x.seance_id,
                        principalTable: "seances",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "ix_absences_centre_id",
                table: "absences",
                column: "centre_id");

            migrationBuilder.CreateIndex(
                name: "ix_absences_inscription_id",
                table: "absences",
                column: "inscription_id");

            migrationBuilder.CreateIndex(
                name: "ix_absences_seance_id_inscription_id",
                table: "absences",
                columns: new[] { "seance_id", "inscription_id" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "absences");

            migrationBuilder.DropColumn(
                name: "appel_fait_le",
                table: "seances");
        }
    }
}
