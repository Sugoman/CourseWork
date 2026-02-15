using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace LearningTrainerShared.Migrations
{
    /// <inheritdoc />
    public partial class AddPerformanceIndexes : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Comments_UserId",
                table: "Comments");

            migrationBuilder.CreateIndex(
                name: "IX_LearningProgresses_UserId_LastPracticed",
                table: "LearningProgresses",
                columns: new[] { "UserId", "LastPracticed" });

            migrationBuilder.CreateIndex(
                name: "IX_LearningProgresses_UserId_NextReview",
                table: "LearningProgresses",
                columns: new[] { "UserId", "NextReview" });

            migrationBuilder.CreateIndex(
                name: "IX_Comments_ContentType_ContentId",
                table: "Comments",
                columns: new[] { "ContentType", "ContentId" });

            migrationBuilder.CreateIndex(
                name: "IX_Comments_UserId_ContentType_ContentId",
                table: "Comments",
                columns: new[] { "UserId", "ContentType", "ContentId" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_LearningProgresses_UserId_LastPracticed",
                table: "LearningProgresses");

            migrationBuilder.DropIndex(
                name: "IX_LearningProgresses_UserId_NextReview",
                table: "LearningProgresses");

            migrationBuilder.DropIndex(
                name: "IX_Comments_ContentType_ContentId",
                table: "Comments");

            migrationBuilder.DropIndex(
                name: "IX_Comments_UserId_ContentType_ContentId",
                table: "Comments");

            migrationBuilder.CreateIndex(
                name: "IX_Comments_UserId",
                table: "Comments",
                column: "UserId");
        }
    }
}
