using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace LearningTrainerShared.Migrations
{
    /// <inheritdoc />
    public partial class AddKnowledgeTree : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "TreeSkins",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Name = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    AssetPrefix = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    StageEmojis = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    IsPremium = table.Column<bool>(type: "bit", nullable: false),
                    PriceCoins = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TreeSkins", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "KnowledgeTrees",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    UserId = table.Column<int>(type: "int", nullable: false),
                    TreeSkinId = table.Column<int>(type: "int", nullable: false, defaultValue: 1),
                    CurrentStage = table.Column<int>(type: "int", nullable: false),
                    TotalWordsContributed = table.Column<int>(type: "int", nullable: false),
                    TotalXpContributed = table.Column<long>(type: "bigint", nullable: false),
                    LastActivityAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_KnowledgeTrees", x => x.Id);
                    table.ForeignKey(
                        name: "FK_KnowledgeTrees_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_KnowledgeTrees_TreeSkins_TreeSkinId",
                        column: x => x.TreeSkinId,
                        principalTable: "TreeSkins",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_KnowledgeTrees_UserId",
                table: "KnowledgeTrees",
                column: "UserId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_KnowledgeTrees_TreeSkinId",
                table: "KnowledgeTrees",
                column: "TreeSkinId");

            // Seed default tree skins
            migrationBuilder.InsertData(
                table: "TreeSkins",
                columns: new[] { "Id", "Name", "AssetPrefix", "StageEmojis", "IsPremium", "PriceCoins" },
                columnTypes: new[] { "int", "nvarchar(50)", "nvarchar(100)", "nvarchar(200)", "bit", "int" },
                values: new object[,]
                {
                    { 1, "Классическое дерево", "default", "🌰|🌱|🌿|🌳|🌲|🏔️|🌍", false, 0 },
                    { 2, "Сакура", "sakura", "🌰|🌸|🌿|🌸|🌸|🌸|🌸", true, 500 },
                    { 3, "Дуб", "oak", "🌰|🌱|🌿|🌳|🌳|🌳|🌳", true, 300 },
                    { 4, "Пальма", "palm", "🥥|🌱|🌿|🌴|🌴|🌴|🌴", true, 400 },
                    { 5, "Ёлка", "pine", "🌰|🌱|🌲|🌲|🎄|🎄|🎄", true, 350 }
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(name: "KnowledgeTrees");
            migrationBuilder.DropTable(name: "TreeSkins");
        }
    }
}
