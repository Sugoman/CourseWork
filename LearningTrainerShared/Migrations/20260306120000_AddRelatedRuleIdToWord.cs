using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace LearningTrainerShared.Migrations
{
    /// <inheritdoc />
    public partial class AddRelatedRuleIdToWord : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "RelatedRuleId",
                table: "Words",
                type: "int",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Words_RelatedRuleId",
                table: "Words",
                column: "RelatedRuleId");

            migrationBuilder.AddForeignKey(
                name: "FK_Words_Rules_RelatedRuleId",
                table: "Words",
                column: "RelatedRuleId",
                principalTable: "Rules",
                principalColumn: "Id",
                onDelete: ReferentialAction.NoAction);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Words_Rules_RelatedRuleId",
                table: "Words");

            migrationBuilder.DropIndex(
                name: "IX_Words_RelatedRuleId",
                table: "Words");

            migrationBuilder.DropColumn(
                name: "RelatedRuleId",
                table: "Words");
        }
    }
}
