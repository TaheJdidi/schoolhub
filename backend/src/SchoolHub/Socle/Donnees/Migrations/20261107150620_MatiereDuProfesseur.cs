using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SchoolHub.Socle.Donnees.Migrations
{
    /// <inheritdoc />
    public partial class MatiereDuProfesseur : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "matiere",
                table: "professeurs",
                type: "character varying(100)",
                maxLength: 100,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "matiere",
                table: "professeurs");
        }
    }
}
