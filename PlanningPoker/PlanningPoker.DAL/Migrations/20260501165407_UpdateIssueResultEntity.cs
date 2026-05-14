using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PlanningPoker.DAL.Migrations
{
    /// <inheritdoc />
    public partial class UpdateIssueResultEntity : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "Code",
                table: "Issues",
                type: "text",
                nullable: false,
                defaultValue: "");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Code",
                table: "Issues");
        }
    }
}
