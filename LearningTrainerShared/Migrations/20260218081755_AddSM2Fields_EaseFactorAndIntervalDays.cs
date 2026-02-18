using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace LearningTrainerShared.Migrations
{
    /// <inheritdoc />
    public partial class AddSM2Fields_EaseFactorAndIntervalDays : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<double>(
                name: "EaseFactor",
                table: "LearningProgresses",
                type: "float",
                nullable: false,
                defaultValue: 2.5);

            migrationBuilder.AddColumn<double>(
                name: "IntervalDays",
                table: "LearningProgresses",
                type: "float",
                nullable: false,
                defaultValue: 0.0);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "EaseFactor",
                table: "LearningProgresses");

            migrationBuilder.DropColumn(
                name: "IntervalDays",
                table: "LearningProgresses");
        }
    }
}
