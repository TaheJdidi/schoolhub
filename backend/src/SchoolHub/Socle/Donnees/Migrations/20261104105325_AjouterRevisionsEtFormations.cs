using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace SchoolHub.Socle.Donnees.Migrations
{
    /// <inheritdoc />
    public partial class AjouterRevisionsEtFormations : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<Guid>(
                name: "annee_scolaire_id",
                table: "groupes",
                type: "uuid",
                nullable: true,
                oldClrType: typeof(Guid),
                oldType: "uuid");

            migrationBuilder.AddColumn<int>(
                name: "nombre_de_seances",
                table: "groupes",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "premiere_seance",
                table: "groupes",
                type: "integer",
                nullable: false,
                defaultValue: 1);

            migrationBuilder.AddColumn<decimal>(
                name: "prix_total",
                table: "groupes",
                type: "numeric(12,3)",
                precision: 12,
                scale: 3,
                nullable: true);

            migrationBuilder.CreateTable(
                name: "tranches_des_groupes",
                columns: table => new
                {
                    groupe_id = table.Column<Guid>(type: "uuid", nullable: false),
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    montant = table.Column<decimal>(type: "numeric(12,3)", precision: 12, scale: 3, nullable: false),
                    seance = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_tranches_des_groupes", x => new { x.groupe_id, x.id });
                    table.ForeignKey(
                        name: "fk_tranches_des_groupes_groupes_groupe_id",
                        column: x => x.groupe_id,
                        principalTable: "groupes",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "tranches_des_groupes");

            migrationBuilder.DropColumn(
                name: "nombre_de_seances",
                table: "groupes");

            migrationBuilder.DropColumn(
                name: "premiere_seance",
                table: "groupes");

            migrationBuilder.DropColumn(
                name: "prix_total",
                table: "groupes");

            migrationBuilder.AlterColumn<Guid>(
                name: "annee_scolaire_id",
                table: "groupes",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"),
                oldClrType: typeof(Guid),
                oldType: "uuid",
                oldNullable: true);
        }
    }
}
