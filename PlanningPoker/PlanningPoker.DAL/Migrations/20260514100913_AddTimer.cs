using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PlanningPoker.DAL.Migrations
{
    /// <inheritdoc />
    public partial class AddTimer : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Votes_Games_GameId",
                table: "Votes");

            migrationBuilder.DropForeignKey(
                name: "FK_VotingResults_Issues_IssueId",
                table: "VotingResults");

            migrationBuilder.DropIndex(
                name: "IX_Votes_GameId",
                table: "Votes");

            migrationBuilder.DropColumn(
                name: "GameId",
                table: "Votes");

            migrationBuilder.RenameColumn(
                name: "FinalEstimate",
                table: "Votes",
                newName: "Estimate");

            migrationBuilder.AddColumn<bool>(
                name: "AutoResetTimer",
                table: "Games",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "CustomValues",
                table: "Games",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "DefaultTimerMinutes",
                table: "Games",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<DateTime>(
                name: "TimerEndsAt",
                table: "Games",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "CanManageIssues",
                table: "GameParticipants",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "CanRevealCards",
                table: "GameParticipants",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.CreateTable(
                name: "GameTimer",
                columns: table => new
                {
                    GameId = table.Column<Guid>(type: "uuid", nullable: false),
                    StartedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    EndsAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    AutoReset = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_GameTimer", x => x.GameId);
                    table.ForeignKey(
                        name: "FK_GameTimer_Games_GameId",
                        column: x => x.GameId,
                        principalTable: "Games",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.AddForeignKey(
                name: "FK_VotingResults_Issues_IssueId",
                table: "VotingResults",
                column: "IssueId",
                principalTable: "Issues",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_VotingResults_Issues_IssueId",
                table: "VotingResults");

            migrationBuilder.DropTable(
                name: "GameTimer");

            migrationBuilder.DropColumn(
                name: "AutoResetTimer",
                table: "Games");

            migrationBuilder.DropColumn(
                name: "CustomValues",
                table: "Games");

            migrationBuilder.DropColumn(
                name: "DefaultTimerMinutes",
                table: "Games");

            migrationBuilder.DropColumn(
                name: "TimerEndsAt",
                table: "Games");

            migrationBuilder.DropColumn(
                name: "CanManageIssues",
                table: "GameParticipants");

            migrationBuilder.DropColumn(
                name: "CanRevealCards",
                table: "GameParticipants");

            migrationBuilder.RenameColumn(
                name: "Estimate",
                table: "Votes",
                newName: "FinalEstimate");

            migrationBuilder.AddColumn<Guid>(
                name: "GameId",
                table: "Votes",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Votes_GameId",
                table: "Votes",
                column: "GameId");

            migrationBuilder.AddForeignKey(
                name: "FK_Votes_Games_GameId",
                table: "Votes",
                column: "GameId",
                principalTable: "Games",
                principalColumn: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_VotingResults_Issues_IssueId",
                table: "VotingResults",
                column: "IssueId",
                principalTable: "Issues",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }
    }
}
