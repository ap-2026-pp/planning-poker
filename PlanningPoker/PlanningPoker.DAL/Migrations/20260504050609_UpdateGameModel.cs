using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PlanningPoker.DAL.Migrations
{
    /// <inheritdoc />
    public partial class UpdateGameModel : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "EnableFunFeatures",
                table: "Games",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<int>(
                name: "IssuesPolicy",
                table: "Games",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "RevealPolicy",
                table: "Games",
                type: "integer",
                nullable: false,
                defaultValue: 0);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "EnableFunFeatures",
                table: "Games");

            migrationBuilder.DropColumn(
                name: "IssuesPolicy",
                table: "Games");

            migrationBuilder.DropColumn(
                name: "RevealPolicy",
                table: "Games");
        }
    }
}
