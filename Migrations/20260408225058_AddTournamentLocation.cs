using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace FairwayManager.Migrations
{
    public partial class AddTournamentLocation : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "City",
                table: "Tournaments",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<double>(
                name: "Latitude",
                table: "Tournaments",
                type: "double precision",
                nullable: true);

            migrationBuilder.AddColumn<double>(
                name: "Longitude",
                table: "Tournaments",
                type: "double precision",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "State",
                table: "Tournaments",
                type: "text",
                nullable: true);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "City",
                table: "Tournaments");

            migrationBuilder.DropColumn(
                name: "Latitude",
                table: "Tournaments");

            migrationBuilder.DropColumn(
                name: "Longitude",
                table: "Tournaments");

            migrationBuilder.DropColumn(
                name: "State",
                table: "Tournaments");
        }
    }
}
