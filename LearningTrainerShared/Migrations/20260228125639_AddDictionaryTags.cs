using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace LearningTrainerShared.Migrations
{
    /// <inheritdoc />
    public partial class AddDictionaryTags : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_DictionarySharings_Dictionaries_DictionaryId1",
                table: "DictionarySharings");

            migrationBuilder.DropIndex(
                name: "IX_DictionarySharings_DictionaryId1",
                table: "DictionarySharings");

            migrationBuilder.DropColumn(
                name: "DictionaryId1",
                table: "DictionarySharings");

            migrationBuilder.AddColumn<string>(
                name: "Tags",
                table: "Dictionaries",
                type: "nvarchar(500)",
                maxLength: 500,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Tags",
                table: "Dictionaries");

            migrationBuilder.AddColumn<int>(
                name: "DictionaryId1",
                table: "DictionarySharings",
                type: "int",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_DictionarySharings_DictionaryId1",
                table: "DictionarySharings",
                column: "DictionaryId1");

            migrationBuilder.AddForeignKey(
                name: "FK_DictionarySharings_Dictionaries_DictionaryId1",
                table: "DictionarySharings",
                column: "DictionaryId1",
                principalTable: "Dictionaries",
                principalColumn: "Id");
        }
    }
}
