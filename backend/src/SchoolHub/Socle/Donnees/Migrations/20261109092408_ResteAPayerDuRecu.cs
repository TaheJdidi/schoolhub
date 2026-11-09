using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SchoolHub.Socle.Donnees.Migrations
{
    /// <inheritdoc />
    public partial class ResteAPayerDuRecu : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<decimal>(
                name: "reste_a_payer",
                table: "recus",
                type: "numeric(12,3)",
                precision: 12,
                scale: 3,
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "reste",
                table: "lignes_des_recus",
                type: "numeric(12,3)",
                precision: 12,
                scale: 3,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "reste_a_payer",
                table: "recus");

            migrationBuilder.DropColumn(
                name: "reste",
                table: "lignes_des_recus");
        }
    }
}
