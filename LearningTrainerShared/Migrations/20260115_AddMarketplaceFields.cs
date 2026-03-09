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
            // Idempotent: all these changes may already exist in the database.
            // Use raw SQL with IF NOT EXISTS guards to avoid "column already exists" errors.

            // Dictionary marketplace fields
            migrationBuilder.Sql(@"
                IF COL_LENGTH('Dictionaries', 'IsPublished') IS NULL
                    ALTER TABLE [Dictionaries] ADD [IsPublished] bit NOT NULL DEFAULT CAST(0 AS bit);");
            migrationBuilder.Sql(@"
                IF COL_LENGTH('Dictionaries', 'Rating') IS NULL
                    ALTER TABLE [Dictionaries] ADD [Rating] float NOT NULL DEFAULT 0;");
            migrationBuilder.Sql(@"
                IF COL_LENGTH('Dictionaries', 'RatingCount') IS NULL
                    ALTER TABLE [Dictionaries] ADD [RatingCount] int NOT NULL DEFAULT 0;");
            migrationBuilder.Sql(@"
                IF COL_LENGTH('Dictionaries', 'DownloadCount') IS NULL
                    ALTER TABLE [Dictionaries] ADD [DownloadCount] int NOT NULL DEFAULT 0;");
            migrationBuilder.Sql(@"
                IF COL_LENGTH('Dictionaries', 'SourceDictionaryId') IS NULL
                    ALTER TABLE [Dictionaries] ADD [SourceDictionaryId] int NULL;");

            // Rule marketplace fields
            migrationBuilder.Sql(@"
                IF COL_LENGTH('Rules', 'IsPublished') IS NULL
                    ALTER TABLE [Rules] ADD [IsPublished] bit NOT NULL DEFAULT CAST(0 AS bit);");
            migrationBuilder.Sql(@"
                IF COL_LENGTH('Rules', 'Rating') IS NULL
                    ALTER TABLE [Rules] ADD [Rating] float NOT NULL DEFAULT 0;");
            migrationBuilder.Sql(@"
                IF COL_LENGTH('Rules', 'RatingCount') IS NULL
                    ALTER TABLE [Rules] ADD [RatingCount] int NOT NULL DEFAULT 0;");
            migrationBuilder.Sql(@"
                IF COL_LENGTH('Rules', 'DownloadCount') IS NULL
                    ALTER TABLE [Rules] ADD [DownloadCount] int NOT NULL DEFAULT 0;");
            migrationBuilder.Sql(@"
                IF COL_LENGTH('Rules', 'SourceRuleId') IS NULL
                    ALTER TABLE [Rules] ADD [SourceRuleId] int NULL;");

            // Comments table
            migrationBuilder.Sql(@"
                IF OBJECT_ID('Comments', 'U') IS NULL
                BEGIN
                    CREATE TABLE [Comments] (
                        [Id] int NOT NULL IDENTITY(1,1),
                        [UserId] int NOT NULL,
                        [ContentType] nvarchar(20) NOT NULL,
                        [ContentId] int NOT NULL,
                        [Rating] int NOT NULL,
                        [Text] nvarchar(1000) NOT NULL,
                        [CreatedAt] datetime2 NOT NULL,
                        CONSTRAINT [PK_Comments] PRIMARY KEY ([Id]),
                        CONSTRAINT [FK_Comments_Users_UserId] FOREIGN KEY ([UserId]) REFERENCES [Users]([Id]) ON DELETE CASCADE
                    );
                    CREATE INDEX [IX_Comments_UserId] ON [Comments]([UserId]);
                    CREATE INDEX [IX_Comments_ContentType_ContentId] ON [Comments]([ContentType], [ContentId]);
                END;");

            // Downloads table
            migrationBuilder.Sql(@"
                IF OBJECT_ID('Downloads', 'U') IS NULL
                BEGIN
                    CREATE TABLE [Downloads] (
                        [Id] int NOT NULL IDENTITY(1,1),
                        [UserId] int NOT NULL,
                        [ContentType] nvarchar(20) NOT NULL,
                        [ContentId] int NOT NULL,
                        [DownloadedAt] datetime2 NOT NULL,
                        CONSTRAINT [PK_Downloads] PRIMARY KEY ([Id]),
                        CONSTRAINT [FK_Downloads_Users_UserId] FOREIGN KEY ([UserId]) REFERENCES [Users]([Id]) ON DELETE CASCADE
                    );
                    CREATE INDEX [IX_Downloads_UserId] ON [Downloads]([UserId]);
                END;");

            // Indexes (idempotent)
            migrationBuilder.Sql(@"
                IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_Dictionaries_IsPublished' AND object_id = OBJECT_ID('Dictionaries'))
                    CREATE INDEX [IX_Dictionaries_IsPublished] ON [Dictionaries]([IsPublished]);");
            migrationBuilder.Sql(@"
                IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_Rules_IsPublished' AND object_id = OBJECT_ID('Rules'))
                    CREATE INDEX [IX_Rules_IsPublished] ON [Rules]([IsPublished]);");
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
