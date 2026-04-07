using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace FairwayManager.Migrations
{
    public partial class RemoveCoursePar : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "CoursePar",
                table: "Tournaments");
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "CoursePar",
                table: "Tournaments",
                type: "integer",
                nullable: false,
                defaultValue: 0);
        }
    }
}
