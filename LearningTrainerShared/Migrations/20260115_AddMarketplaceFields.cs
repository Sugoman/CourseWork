using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace LearningTrainerShared.Migrations
{
    /// <inheritdoc />
    public partial class AddMarketplaceFields : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Dictionary marketplace fields
            migrationBuilder.AddColumn<bool>(
                name: "IsPublished",
                table: "Dictionaries",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<double>(
                name: "Rating",
                table: "Dictionaries",
                type: "float",
                nullable: false,
                defaultValue: 0.0);

            migrationBuilder.AddColumn<int>(
                name: "RatingCount",
                table: "Dictionaries",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "DownloadCount",
                table: "Dictionaries",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "SourceDictionaryId",
                table: "Dictionaries",
                type: "int",
                nullable: true);

            // Rule marketplace fields
            migrationBuilder.AddColumn<bool>(
                name: "IsPublished",
                table: "Rules",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<double>(
                name: "Rating",
                table: "Rules",
                type: "float",
                nullable: false,
                defaultValue: 0.0);

            migrationBuilder.AddColumn<int>(
                name: "RatingCount",
                table: "Rules",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "DownloadCount",
                table: "Rules",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "SourceRuleId",
                table: "Rules",
                type: "int",
                nullable: true);

            // Comments table
            migrationBuilder.CreateTable(
                name: "Comments",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    UserId = table.Column<int>(type: "int", nullable: false),
                    ContentType = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    ContentId = table.Column<int>(type: "int", nullable: false),
                    Rating = table.Column<int>(type: "int", nullable: false),
                    Text = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Comments", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Comments_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            // Downloads table
            migrationBuilder.CreateTable(
                name: "Downloads",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    UserId = table.Column<int>(type: "int", nullable: false),
                    ContentType = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    ContentId = table.Column<int>(type: "int", nullable: false),
                    DownloadedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Downloads", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Downloads_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            // Indexes
            migrationBuilder.CreateIndex(
                name: "IX_Comments_UserId",
                table: "Comments",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_Comments_ContentType_ContentId",
                table: "Comments",
                columns: new[] { "ContentType", "ContentId" });

            migrationBuilder.CreateIndex(
                name: "IX_Downloads_UserId",
                table: "Downloads",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_Dictionaries_IsPublished",
                table: "Dictionaries",
                column: "IsPublished");

            migrationBuilder.CreateIndex(
                name: "IX_Rules_IsPublished",
                table: "Rules",
                column: "IsPublished");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(name: "Comments");
            migrationBuilder.DropTable(name: "Downloads");

            migrationBuilder.DropIndex(name: "IX_Dictionaries_IsPublished", table: "Dictionaries");
            migrationBuilder.DropIndex(name: "IX_Rules_IsPublished", table: "Rules");

            migrationBuilder.DropColumn(name: "IsPublished", table: "Dictionaries");
            migrationBuilder.DropColumn(name: "Rating", table: "Dictionaries");
            migrationBuilder.DropColumn(name: "RatingCount", table: "Dictionaries");
            migrationBuilder.DropColumn(name: "DownloadCount", table: "Dictionaries");
            migrationBuilder.DropColumn(name: "SourceDictionaryId", table: "Dictionaries");

            migrationBuilder.DropColumn(name: "IsPublished", table: "Rules");
            migrationBuilder.DropColumn(name: "Rating", table: "Rules");
            migrationBuilder.DropColumn(name: "RatingCount", table: "Rules");
            migrationBuilder.DropColumn(name: "DownloadCount", table: "Rules");
            migrationBuilder.DropColumn(name: "SourceRuleId", table: "Rules");
        }
    }
}
