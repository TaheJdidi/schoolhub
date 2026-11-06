using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SchoolHub.Socle.Donnees.Migrations
{
    /// <inheritdoc />
    public partial class AjouterLesRattrapages : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "rattrapages",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    centre_id = table.Column<Guid>(type: "uuid", nullable: false),
                    seance_id = table.Column<Guid>(type: "uuid", nullable: false),
                    inscription_id = table.Column<Guid>(type: "uuid", nullable: false),
                    seance_rattrapee_id = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_rattrapages", x => x.id);
                    table.ForeignKey(
                        name: "fk_rattrapages_centres_centre_id",
                        column: x => x.centre_id,
                        principalTable: "centres",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_rattrapages_inscriptions_inscription_id",
                        column: x => x.inscription_id,
                        principalTable: "inscriptions",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_rattrapages_seances_seance_id",
                        column: x => x.seance_id,
                        principalTable: "seances",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "fk_rattrapages_seances_seance_rattrapee_id",
                        column: x => x.seance_rattrapee_id,
                        principalTable: "seances",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "ix_rattrapages_centre_id",
                table: "rattrapages",
                column: "centre_id");

            migrationBuilder.CreateIndex(
                name: "ix_rattrapages_inscription_id_seance_rattrapee_id",
                table: "rattrapages",
                columns: new[] { "inscription_id", "seance_rattrapee_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_rattrapages_seance_id_inscription_id",
                table: "rattrapages",
                columns: new[] { "seance_id", "inscription_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_rattrapages_seance_rattrapee_id",
                table: "rattrapages",
                column: "seance_rattrapee_id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "rattrapages");
        }
    }
}
