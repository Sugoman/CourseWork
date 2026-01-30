using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace LearningTrainerShared.Migrations
{
    /// <inheritdoc />
    public partial class RenameLoginToUsername : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Переименовываем колонку Login в Username
            migrationBuilder.RenameColumn(
                name: "Login",
                table: "Users",
                newName: "Username");

            // Делаем Email обязательным (NOT NULL)
            // Сначала обновляем существующие NULL значения
            migrationBuilder.Sql("UPDATE Users SET Email = Username WHERE Email IS NULL OR Email = ''");

            // Теперь меняем колонку на NOT NULL
            migrationBuilder.AlterColumn<string>(
                name: "Email",
                table: "Users",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: false,
                defaultValue: "",
                oldClrType: typeof(string),
                oldType: "nvarchar(100)",
                oldMaxLength: 100,
                oldNullable: true);

            // Добавляем уникальный индекс на Email
            migrationBuilder.CreateIndex(
                name: "IX_Users_Email",
                table: "Users",
                column: "Email",
                unique: true);

            // Добавляем уникальный индекс на Username
            migrationBuilder.CreateIndex(
                name: "IX_Users_Username",
                table: "Users",
                column: "Username",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Удаляем индексы
            migrationBuilder.DropIndex(
                name: "IX_Users_Email",
                table: "Users");

            migrationBuilder.DropIndex(
                name: "IX_Users_Username",
                table: "Users");

            // Возвращаем Email к nullable
            migrationBuilder.AlterColumn<string>(
                name: "Email",
                table: "Users",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "nvarchar(100)",
                oldMaxLength: 100);

            // Переименовываем обратно
            migrationBuilder.RenameColumn(
                name: "Username",
                table: "Users",
                newName: "Login");
        }
    }
}
