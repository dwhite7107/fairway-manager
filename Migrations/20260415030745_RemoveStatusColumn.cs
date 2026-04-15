using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace FairwayManager.Migrations
{
    public partial class RemoveStatusColumn : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Status",
                table: "Tournaments");
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "Status",
                table: "Tournaments",
                type: "text",
                nullable: false,
                defaultValue: "");
        }
    }
}
