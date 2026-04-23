using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace FairwayManager.Migrations
{
    public partial class AddQualificationsToTournament : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "Qualifications",
                table: "Tournaments",
                type: "text",
                nullable: true);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Qualifications",
                table: "Tournaments");
        }
    }
}
