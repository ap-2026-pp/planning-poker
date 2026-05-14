using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PlanningPoker.DAL.Migrations
{
    /// <inheritdoc />
    public partial class FixMigrations : Migration
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

            migrationBuilder.AddColumn<DateTime>(
                name: "TimerEndsAt",
                table: "Games",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AlterColumn<Guid>(
                name: "UserId",
                table: "GameParticipants",
                type: "uuid",
                nullable: true,
                oldClrType: typeof(Guid),
                oldType: "uuid");

            migrationBuilder.CreateTable(
                name: "GuestSessions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ParticipantId = table.Column<Guid>(type: "uuid", nullable: false),
                    TokenHash = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    ExpiresAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    IsRevoked = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_GuestSessions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_GuestSessions_GameParticipants_ParticipantId",
                        column: x => x.ParticipantId,
                        principalTable: "GameParticipants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_GuestSessions_ParticipantId",
                table: "GuestSessions",
                column: "ParticipantId");

            migrationBuilder.CreateIndex(
                name: "IX_GuestSessions_TokenHash",
                table: "GuestSessions",
                column: "TokenHash",
                unique: true);

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

            migrationBuilder.DropTable(
                name: "GuestSessions");

            migrationBuilder.DropColumn(
                name: "TimerEndsAt",
                table: "Games");

            migrationBuilder.AddColumn<Guid>(
                name: "GameId",
                table: "Votes",
                type: "uuid",
                nullable: true);

            migrationBuilder.AlterColumn<Guid>(
                name: "UserId",
                table: "GameParticipants",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"),
                oldClrType: typeof(Guid),
                oldType: "uuid",
                oldNullable: true);

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
