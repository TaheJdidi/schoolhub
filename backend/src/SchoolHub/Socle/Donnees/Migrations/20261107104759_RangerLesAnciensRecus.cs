using Microsoft.EntityFrameworkCore.Migrations;
using SchoolHub.Modules.Caisse;

#nullable disable

namespace SchoolHub.Socle.Donnees.Migrations
{
    /// <summary>
    /// CAI-09 : range les reçus et les sorties de caisse des jours passés, encore ouverts, dans une clôture « historique »
    /// par compte et par jour (voir <see cref="CloturesHistoriques"/>). Sans elle, chaque jour d'avant la clôture du soir
    /// deviendrait une caisse « pas clôturée ».
    /// </summary>
    public partial class RangerLesAnciensRecus : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(CloturesHistoriques.Sql(CloturesHistoriques.AujourdhuiATunis));
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Les clôtures historiques ne se distinguent plus des autres : rien n'est défait.
        }
    }
}
