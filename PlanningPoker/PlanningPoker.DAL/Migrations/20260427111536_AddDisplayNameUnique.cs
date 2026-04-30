using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PlanningPoker.DAL.Migrations
{
    /// <inheritdoc />
    public partial class AddDisplayNameUnique : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_GameParticipants_GameId",
                table: "GameParticipants");

            migrationBuilder.CreateIndex(
                name: "IX_GameParticipants_GameId_DisplayName",
                table: "GameParticipants",
                columns: new[] { "GameId", "DisplayName" },
                unique: true,
                filter: "\"RemovedAt\" IS NULL");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_GameParticipants_GameId_DisplayName",
                table: "GameParticipants");

            migrationBuilder.CreateIndex(
                name: "IX_GameParticipants_GameId",
                table: "GameParticipants",
                column: "GameId");
        }
    }
}
