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
            // Idempotent: these changes may already exist in the database.

            // Rename Login → Username (only if Login still exists)
            migrationBuilder.Sql(@"
                IF COL_LENGTH('Users', 'Login') IS NOT NULL AND COL_LENGTH('Users', 'Username') IS NULL
                    EXEC sp_rename 'Users.Login', 'Username', 'COLUMN';");

            // Make Email NOT NULL (fill empty values first)
            migrationBuilder.Sql(@"
                UPDATE Users SET Email = Username WHERE Email IS NULL OR Email = '';");
            migrationBuilder.Sql(@"
                IF EXISTS (
                    SELECT 1 FROM INFORMATION_SCHEMA.COLUMNS
                    WHERE TABLE_NAME = 'Users' AND COLUMN_NAME = 'Email' AND IS_NULLABLE = 'YES'
                )
                    ALTER TABLE [Users] ALTER COLUMN [Email] nvarchar(100) NOT NULL;");

            // Unique indexes (idempotent)
            migrationBuilder.Sql(@"
                IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_Users_Email' AND object_id = OBJECT_ID('Users'))
                    CREATE UNIQUE INDEX [IX_Users_Email] ON [Users]([Email]);");
            migrationBuilder.Sql(@"
                IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_Users_Username' AND object_id = OBJECT_ID('Users'))
                    CREATE UNIQUE INDEX [IX_Users_Username] ON [Users]([Username]);");
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
