using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace LearningTrainerShared.Migrations
{
    /// <inheritdoc />
    public partial class DBUpdate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
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

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
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
        }
    }
}
