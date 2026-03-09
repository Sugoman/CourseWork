using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace LearningTrainerShared.Migrations
{
    /// <summary>
    /// §17 LEARNING_IMPROVEMENTS: Переработка системы «Правила» → Интерактивная грамматика.
    /// - Добавляет таблицу GrammarProgress (SRS для правил).
    /// - Расширяет Rule полями skill tree (SkillTreeLevel, PrerequisiteRuleIdsJson, IconEmoji, SkillSummary, XpReward).
    /// - Расширяет GrammarExercise полями для новых типов упражнений (ExerciseType, CorrectAnswer, AlternativeAnswersJson, IncorrectSentence, ShuffledWordsJson, DifficultyTier).
    /// </summary>
    public partial class AddGrammarProgressAndSkillTree : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Idempotent: use IF NOT EXISTS guards so migration can re-run safely.

            // === Rule: skill tree fields ===
            migrationBuilder.Sql(@"
                IF COL_LENGTH('Rules', 'SkillTreeLevel') IS NULL
                    ALTER TABLE [Rules] ADD [SkillTreeLevel] int NOT NULL DEFAULT 1;");
            migrationBuilder.Sql(@"
                IF COL_LENGTH('Rules', 'PrerequisiteRuleIdsJson') IS NULL
                    ALTER TABLE [Rules] ADD [PrerequisiteRuleIdsJson] nvarchar(max) NULL;");
            migrationBuilder.Sql(@"
                IF COL_LENGTH('Rules', 'IconEmoji') IS NULL
                    ALTER TABLE [Rules] ADD [IconEmoji] nvarchar(10) NULL;");
            migrationBuilder.Sql(@"
                IF COL_LENGTH('Rules', 'SkillSummary') IS NULL
                    ALTER TABLE [Rules] ADD [SkillSummary] nvarchar(100) NULL;");
            migrationBuilder.Sql(@"
                IF COL_LENGTH('Rules', 'XpReward') IS NULL
                    ALTER TABLE [Rules] ADD [XpReward] int NOT NULL DEFAULT 50;");

            // === GrammarExercise: new exercise type fields ===
            migrationBuilder.Sql(@"
                IF COL_LENGTH('GrammarExercises', 'ExerciseType') IS NULL
                    ALTER TABLE [GrammarExercises] ADD [ExerciseType] nvarchar(30) NOT NULL DEFAULT 'mcq';");
            migrationBuilder.Sql(@"
                IF COL_LENGTH('GrammarExercises', 'CorrectAnswer') IS NULL
                    ALTER TABLE [GrammarExercises] ADD [CorrectAnswer] nvarchar(500) NULL;");
            migrationBuilder.Sql(@"
                IF COL_LENGTH('GrammarExercises', 'AlternativeAnswersJson') IS NULL
                    ALTER TABLE [GrammarExercises] ADD [AlternativeAnswersJson] nvarchar(max) NULL;");
            migrationBuilder.Sql(@"
                IF COL_LENGTH('GrammarExercises', 'IncorrectSentence') IS NULL
                    ALTER TABLE [GrammarExercises] ADD [IncorrectSentence] nvarchar(500) NULL;");
            migrationBuilder.Sql(@"
                IF COL_LENGTH('GrammarExercises', 'ShuffledWordsJson') IS NULL
                    ALTER TABLE [GrammarExercises] ADD [ShuffledWordsJson] nvarchar(max) NULL;");
            migrationBuilder.Sql(@"
                IF COL_LENGTH('GrammarExercises', 'DifficultyTier') IS NULL
                    ALTER TABLE [GrammarExercises] ADD [DifficultyTier] int NOT NULL DEFAULT 1;");

            // === GrammarProgress: new table ===
            migrationBuilder.Sql(@"
                IF OBJECT_ID('GrammarProgresses', 'U') IS NULL
                BEGIN
                    CREATE TABLE [GrammarProgresses] (
                        [Id] int NOT NULL IDENTITY(1,1),
                        [UserId] int NOT NULL,
                        [RuleId] int NOT NULL,
                        [KnowledgeLevel] int NOT NULL DEFAULT 0,
                        [EaseFactor] float NOT NULL DEFAULT 2.5,
                        [IntervalDays] float NOT NULL DEFAULT 0,
                        [NextReview] datetime2 NULL,
                        [TotalSessions] int NOT NULL DEFAULT 0,
                        [CorrectAnswers] int NOT NULL DEFAULT 0,
                        [TotalAnswers] int NOT NULL DEFAULT 0,
                        [LastPracticeDate] datetime2 NULL,
                        [LapseCount] int NOT NULL DEFAULT 0,
                        CONSTRAINT [PK_GrammarProgresses] PRIMARY KEY ([Id]),
                        CONSTRAINT [AK_GrammarProgresses_UserId_RuleId] UNIQUE ([UserId], [RuleId]),
                        CONSTRAINT [FK_GrammarProgresses_Users_UserId] FOREIGN KEY ([UserId]) REFERENCES [Users]([Id]) ON DELETE NO ACTION,
                        CONSTRAINT [FK_GrammarProgresses_Rules_RuleId] FOREIGN KEY ([RuleId]) REFERENCES [Rules]([Id]) ON DELETE CASCADE
                    );
                    CREATE INDEX [IX_GrammarProgresses_UserId_NextReview] ON [GrammarProgresses]([UserId], [NextReview]);
                    CREATE INDEX [IX_GrammarProgresses_RuleId] ON [GrammarProgresses]([RuleId]);
                END;");

            // Migrate existing Rules: SkillTreeLevel = DifficultyLevel
            migrationBuilder.Sql("UPDATE Rules SET SkillTreeLevel = DifficultyLevel WHERE SkillTreeLevel = 1 AND DifficultyLevel > 1");
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(name: "GrammarProgresses");

            migrationBuilder.DropColumn(name: "SkillTreeLevel", table: "Rules");
            migrationBuilder.DropColumn(name: "PrerequisiteRuleIdsJson", table: "Rules");
            migrationBuilder.DropColumn(name: "IconEmoji", table: "Rules");
            migrationBuilder.DropColumn(name: "SkillSummary", table: "Rules");
            migrationBuilder.DropColumn(name: "XpReward", table: "Rules");

            migrationBuilder.DropColumn(name: "ExerciseType", table: "GrammarExercises");
            migrationBuilder.DropColumn(name: "CorrectAnswer", table: "GrammarExercises");
            migrationBuilder.DropColumn(name: "AlternativeAnswersJson", table: "GrammarExercises");
            migrationBuilder.DropColumn(name: "IncorrectSentence", table: "GrammarExercises");
            migrationBuilder.DropColumn(name: "ShuffledWordsJson", table: "GrammarExercises");
            migrationBuilder.DropColumn(name: "DifficultyTier", table: "GrammarExercises");
        }
    }
}
