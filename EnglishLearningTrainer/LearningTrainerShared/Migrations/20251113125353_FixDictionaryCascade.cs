using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace LearningTrainerShared.Migrations
{
    /// <inheritdoc />
    public partial class FixDictionaryCascade : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Words_Dictionaries_DictionaryId",
                table: "Words");

            migrationBuilder.AddForeignKey(
                name: "FK_Words_Dictionaries_DictionaryId",
                table: "Words",
                column: "DictionaryId",
                principalTable: "Dictionaries",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Words_Dictionaries_DictionaryId",
                table: "Words");

            migrationBuilder.AddForeignKey(
                name: "FK_Words_Dictionaries_DictionaryId",
                table: "Words",
                column: "DictionaryId",
                principalTable: "Dictionaries",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }
    }
}
