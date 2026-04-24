using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PlanningPoker.DAL.Migrations
{
    /// <inheritdoc />
    public partial class AddGameSoftDelete : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateIndex(
                name: "IX_Games_Name_CreatedBy",
                table: "Games",
                columns: new[] { "Name", "CreatedBy" },
                unique: true,
                filter: "\"IsDeleted\" = false");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Games_Name_CreatedBy",
                table: "Games");
        }
    }
}
