using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace LearningTrainerShared.Migrations
{
    /// <inheritdoc />
    public partial class AddTreeXpColumn : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<long>(
                name: "TreeXp",
                table: "KnowledgeTrees",
                type: "bigint",
                nullable: false,
                defaultValue: 0L);

            migrationBuilder.UpdateData(
                table: "TreeSkins",
                keyColumn: "Id",
                keyValue: 2,
                column: "StageEmojis",
                value: "🌰|🌸|🌿|🌸|🌸|🌸|🌸");

            migrationBuilder.UpdateData(
                table: "TreeSkins",
                keyColumn: "Id",
                keyValue: 4,
                column: "StageEmojis",
                value: "🥥|🌱|🌿|🌴|🌴|🌴|🌴");

            migrationBuilder.UpdateData(
                table: "TreeSkins",
                keyColumn: "Id",
                keyValue: 5,
                column: "StageEmojis",
                value: "🌰|🌱|🌲|🌲|🎄|🎄|🎄");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "TreeXp",
                table: "KnowledgeTrees");

            migrationBuilder.UpdateData(
                table: "TreeSkins",
                keyColumn: "Id",
                keyValue: 2,
                column: "StageEmojis",
                value: "🌸|🌸|🌸|🌸|🌸|🌸|🌸");

            migrationBuilder.UpdateData(
                table: "TreeSkins",
                keyColumn: "Id",
                keyValue: 4,
                column: "StageEmojis",
                value: "🥥|🌴|🌴|🌴|🌴|🌴|🌴");

            migrationBuilder.UpdateData(
                table: "TreeSkins",
                keyColumn: "Id",
                keyValue: 5,
                column: "StageEmojis",
                value: "🌲|🌲|🌲|🌲|🌲|🌲|🌲");
        }
    }
}
