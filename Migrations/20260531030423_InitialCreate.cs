using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TelegramInterviewBot.Migrations
{
    /// <inheritdoc />
    public partial class InitialCreate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "AskedQuestions",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    QuestionText = table.Column<string>(type: "TEXT", nullable: false),
                    VectorEmbedding = table.Column<byte[]>(type: "BLOB", nullable: false),
                    ModelSolution = table.Column<string>(type: "TEXT", nullable: false),
                    AskedOn = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AskedQuestions", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Settings",
                columns: table => new
                {
                    Key = table.Column<string>(type: "TEXT", nullable: false),
                    Value = table.Column<string>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Settings", x => x.Key);
                });

            migrationBuilder.CreateTable(
                name: "Users",
                columns: table => new
                {
                    TelegramId = table.Column<long>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    Name = table.Column<string>(type: "TEXT", nullable: false),
                    Score = table.Column<int>(type: "INTEGER", nullable: false),
                    IsSubscribed = table.Column<int>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Users", x => x.TelegramId);
                });

            migrationBuilder.CreateTable(
                name: "AnswerHistories",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    TelegramId = table.Column<long>(type: "INTEGER", nullable: false),
                    QuestionId = table.Column<int>(type: "INTEGER", nullable: false),
                    GivenAnswer = table.Column<string>(type: "TEXT", nullable: false),
                    ScoreReceived = table.Column<int>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AnswerHistories", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AnswerHistories_AskedQuestions_QuestionId",
                        column: x => x.QuestionId,
                        principalTable: "AskedQuestions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_AnswerHistories_Users_TelegramId",
                        column: x => x.TelegramId,
                        principalTable: "Users",
                        principalColumn: "TelegramId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_AnswerHistories_QuestionId",
                table: "AnswerHistories",
                column: "QuestionId");

            migrationBuilder.CreateIndex(
                name: "IX_AnswerHistories_TelegramId_QuestionId",
                table: "AnswerHistories",
                columns: new[] { "TelegramId", "QuestionId" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AnswerHistories");

            migrationBuilder.DropTable(
                name: "Settings");

            migrationBuilder.DropTable(
                name: "AskedQuestions");

            migrationBuilder.DropTable(
                name: "Users");
        }
    }
}
