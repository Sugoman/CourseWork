using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace LearningTrainerShared.Migrations
{
    /// <inheritdoc />
    public partial class Init : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Roles",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Name = table.Column<string>(type: "nvarchar(max)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Roles", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "TranscriptionCache",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    WordLower = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    Transcription = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TranscriptionCache", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "TreeSkins",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Name = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    AssetPrefix = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    StageEmojis = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    IsPremium = table.Column<bool>(type: "bit", nullable: false),
                    PriceCoins = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TreeSkins", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Users",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    RoleId = table.Column<int>(type: "int", nullable: false),
                    Username = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    Email = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    PasswordHash = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    RefreshToken = table.Column<string>(type: "nvarchar(450)", nullable: true),
                    RefreshTokenExpiryTime = table.Column<DateTime>(type: "datetime2", nullable: true),
                    IsRefreshTokenRevoked = table.Column<bool>(type: "bit", nullable: false),
                    UserId = table.Column<int>(type: "int", nullable: true),
                    InviteCode = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    TermsAcceptedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    TermsVersion = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: true),
                    TimeZoneId = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Users", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Users_Roles_RoleId",
                        column: x => x.RoleId,
                        principalTable: "Roles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_Users_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "Id");
                });

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

            migrationBuilder.CreateTable(
                name: "Dictionaries",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    UserId = table.Column<int>(type: "int", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    Description = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                    LanguageFrom = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    LanguageTo = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    IsPublished = table.Column<bool>(type: "bit", nullable: false),
                    Rating = table.Column<double>(type: "float", nullable: false),
                    RatingCount = table.Column<int>(type: "int", nullable: false),
                    DownloadCount = table.Column<int>(type: "int", nullable: false),
                    SourceDictionaryId = table.Column<int>(type: "int", nullable: true),
                    Tags = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Dictionaries", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Dictionaries_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

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

            migrationBuilder.CreateTable(
                name: "KnowledgeTrees",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    UserId = table.Column<int>(type: "int", nullable: false),
                    TreeSkinId = table.Column<int>(type: "int", nullable: false),
                    CurrentStage = table.Column<int>(type: "int", nullable: false),
                    TotalWordsContributed = table.Column<int>(type: "int", nullable: false),
                    TotalXpContributed = table.Column<long>(type: "bigint", nullable: false),
                    TreeXp = table.Column<long>(type: "bigint", nullable: false),
                    LastActivityAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_KnowledgeTrees", x => x.Id);
                    table.ForeignKey(
                        name: "FK_KnowledgeTrees_TreeSkins_TreeSkinId",
                        column: x => x.TreeSkinId,
                        principalTable: "TreeSkins",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_KnowledgeTrees_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "Rules",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    UserId = table.Column<int>(type: "int", nullable: false),
                    Title = table.Column<string>(type: "nvarchar(70)", maxLength: 70, nullable: false),
                    MarkdownContent = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Description = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Category = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    DifficultyLevel = table.Column<int>(type: "int", nullable: false),
                    IsPublished = table.Column<bool>(type: "bit", nullable: false),
                    Rating = table.Column<double>(type: "float", nullable: false),
                    RatingCount = table.Column<int>(type: "int", nullable: false),
                    DownloadCount = table.Column<int>(type: "int", nullable: false),
                    SourceRuleId = table.Column<int>(type: "int", nullable: true),
                    SkillTreeLevel = table.Column<int>(type: "int", nullable: false),
                    PrerequisiteRuleIdsJson = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    IconEmoji = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: true),
                    SkillSummary = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    XpReward = table.Column<int>(type: "int", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Rules", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Rules_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "UserAchievements",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    UserId = table.Column<int>(type: "int", nullable: false),
                    AchievementId = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    UnlockedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CurrentProgress = table.Column<int>(type: "int", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_UserAchievements", x => x.Id);
                    table.ForeignKey(
                        name: "FK_UserAchievements_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "UserStats",
                columns: table => new
                {
                    UserId = table.Column<int>(type: "int", nullable: false),
                    CurrentStreak = table.Column<int>(type: "int", nullable: false),
                    BestStreak = table.Column<int>(type: "int", nullable: false),
                    LastPracticeDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    StreakFreezeCount = table.Column<int>(type: "int", nullable: false),
                    LastFreezeUsedDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    TotalLearningTimeSeconds = table.Column<long>(type: "bigint", nullable: false),
                    TotalSessions = table.Column<int>(type: "int", nullable: false),
                    DailyGoal = table.Column<int>(type: "int", nullable: false),
                    TotalXp = table.Column<long>(type: "bigint", nullable: false),
                    LastUpdated = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_UserStats", x => x.UserId);
                    table.ForeignKey(
                        name: "FK_UserStats_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "DictionarySharings",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    DictionaryId = table.Column<int>(type: "int", nullable: false),
                    StudentId = table.Column<int>(type: "int", nullable: false),
                    SharedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DictionarySharings", x => x.Id);
                    table.ForeignKey(
                        name: "FK_DictionarySharings_Dictionaries_DictionaryId",
                        column: x => x.DictionaryId,
                        principalTable: "Dictionaries",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_DictionarySharings_Users_StudentId",
                        column: x => x.StudentId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "TrainingSessions",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    UserId = table.Column<int>(type: "int", nullable: false),
                    StartedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CompletedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    WordsReviewed = table.Column<int>(type: "int", nullable: false),
                    CorrectAnswers = table.Column<int>(type: "int", nullable: false),
                    WrongAnswers = table.Column<int>(type: "int", nullable: false),
                    Mode = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    DictionaryId = table.Column<int>(type: "int", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TrainingSessions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_TrainingSessions_Dictionaries_DictionaryId",
                        column: x => x.DictionaryId,
                        principalTable: "Dictionaries",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_TrainingSessions_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "GrammarExercises",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    RuleId = table.Column<int>(type: "int", nullable: false),
                    ExerciseType = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false),
                    Question = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                    OptionsJson = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    CorrectIndex = table.Column<int>(type: "int", nullable: false),
                    CorrectAnswer = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    AlternativeAnswersJson = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    IncorrectSentence = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    ShuffledWordsJson = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Explanation = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: false),
                    OrderIndex = table.Column<int>(type: "int", nullable: false),
                    DifficultyTier = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_GrammarExercises", x => x.Id);
                    table.ForeignKey(
                        name: "FK_GrammarExercises_Rules_RuleId",
                        column: x => x.RuleId,
                        principalTable: "Rules",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "GrammarProgresses",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    UserId = table.Column<int>(type: "int", nullable: false),
                    RuleId = table.Column<int>(type: "int", nullable: false),
                    KnowledgeLevel = table.Column<int>(type: "int", nullable: false),
                    EaseFactor = table.Column<double>(type: "float", nullable: false),
                    IntervalDays = table.Column<double>(type: "float", nullable: false),
                    NextReview = table.Column<DateTime>(type: "datetime2", nullable: true),
                    TotalSessions = table.Column<int>(type: "int", nullable: false),
                    CorrectAnswers = table.Column<int>(type: "int", nullable: false),
                    TotalAnswers = table.Column<int>(type: "int", nullable: false),
                    LastPracticeDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    LapseCount = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_GrammarProgresses", x => x.Id);
                    table.UniqueConstraint("AK_GrammarProgresses_UserId_RuleId", x => new { x.UserId, x.RuleId });
                    table.ForeignKey(
                        name: "FK_GrammarProgresses_Rules_RuleId",
                        column: x => x.RuleId,
                        principalTable: "Rules",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_GrammarProgresses_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "RuleSharings",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    RuleId = table.Column<int>(type: "int", nullable: false),
                    StudentId = table.Column<int>(type: "int", nullable: false),
                    SharedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RuleSharings", x => x.Id);
                    table.ForeignKey(
                        name: "FK_RuleSharings_Rules_RuleId",
                        column: x => x.RuleId,
                        principalTable: "Rules",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_RuleSharings_Users_StudentId",
                        column: x => x.StudentId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "Words",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    UserId = table.Column<int>(type: "int", nullable: false),
                    OriginalWord = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    Translation = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    Example = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                    DictionaryId = table.Column<int>(type: "int", nullable: false),
                    AddedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    RelatedRuleId = table.Column<int>(type: "int", nullable: true),
                    Transcription = table.Column<string>(type: "nvarchar(max)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Words", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Words_Dictionaries_DictionaryId",
                        column: x => x.DictionaryId,
                        principalTable: "Dictionaries",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Words_Rules_RelatedRuleId",
                        column: x => x.RelatedRuleId,
                        principalTable: "Rules",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_Words_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "LearningProgresses",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    UserId = table.Column<int>(type: "int", nullable: false),
                    WordId = table.Column<int>(type: "int", nullable: false),
                    CorrectAnswers = table.Column<int>(type: "int", nullable: false),
                    TotalAttempts = table.Column<int>(type: "int", nullable: false),
                    KnowledgeLevel = table.Column<int>(type: "int", nullable: false),
                    EaseFactor = table.Column<double>(type: "float", nullable: false),
                    IntervalDays = table.Column<double>(type: "float", nullable: false),
                    LastPracticed = table.Column<DateTime>(type: "datetime2", nullable: false),
                    NextReview = table.Column<DateTime>(type: "datetime2", nullable: false),
                    LapseCount = table.Column<int>(type: "int", nullable: false),
                    IsSuspended = table.Column<bool>(type: "bit", nullable: false),
                    LastResponseTimeMs = table.Column<int>(type: "int", nullable: true),
                    UserNote = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_LearningProgresses", x => x.Id);
                    table.UniqueConstraint("AK_LearningProgresses_UserId_WordId", x => new { x.UserId, x.WordId });
                    table.ForeignKey(
                        name: "FK_LearningProgresses_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_LearningProgresses_Words_WordId",
                        column: x => x.WordId,
                        principalTable: "Words",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.InsertData(
                table: "Roles",
                columns: new[] { "Id", "Name" },
                values: new object[,]
                {
                    { 1, "Admin" },
                    { 2, "Teacher" },
                    { 3, "Student" },
                    { 4, "User" }
                });

            migrationBuilder.InsertData(
                table: "TreeSkins",
                columns: new[] { "Id", "AssetPrefix", "IsPremium", "Name", "PriceCoins", "StageEmojis" },
                values: new object[,]
                {
                    { 1, "default", false, "Классическое дерево", 0, "🌰|🌱|🌿|🌳|🌲|🏔️|🌍" },
                    { 2, "sakura", true, "Сакура", 500, "🌰|🌸|🌿|🌸|🌸|🌸|🌸" },
                    { 3, "oak", true, "Дуб", 300, "🌰|🌱|🌿|🌳|🌳|🌳|🌳" },
                    { 4, "palm", true, "Пальма", 400, "🥥|🌱|🌿|🌴|🌴|🌴|🌴" },
                    { 5, "pine", true, "Ёлка", 350, "🌰|🌱|🌲|🌲|🎄|🎄|🎄" }
                });

            migrationBuilder.CreateIndex(
                name: "IX_Comments_ContentType_ContentId",
                table: "Comments",
                columns: new[] { "ContentType", "ContentId" });

            migrationBuilder.CreateIndex(
                name: "IX_Comments_UserId_ContentType_ContentId",
                table: "Comments",
                columns: new[] { "UserId", "ContentType", "ContentId" });

            migrationBuilder.CreateIndex(
                name: "IX_Dictionaries_UserId",
                table: "Dictionaries",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_DictionarySharings_DictionaryId",
                table: "DictionarySharings",
                column: "DictionaryId");

            migrationBuilder.CreateIndex(
                name: "IX_DictionarySharings_StudentId",
                table: "DictionarySharings",
                column: "StudentId");

            migrationBuilder.CreateIndex(
                name: "IX_Downloads_UserId",
                table: "Downloads",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_GrammarExercises_RuleId",
                table: "GrammarExercises",
                column: "RuleId");

            migrationBuilder.CreateIndex(
                name: "IX_GrammarProgresses_RuleId",
                table: "GrammarProgresses",
                column: "RuleId");

            migrationBuilder.CreateIndex(
                name: "IX_GrammarProgresses_UserId_NextReview",
                table: "GrammarProgresses",
                columns: new[] { "UserId", "NextReview" });

            migrationBuilder.CreateIndex(
                name: "IX_KnowledgeTrees_TreeSkinId",
                table: "KnowledgeTrees",
                column: "TreeSkinId");

            migrationBuilder.CreateIndex(
                name: "IX_KnowledgeTrees_UserId",
                table: "KnowledgeTrees",
                column: "UserId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_LearningProgresses_NextReview",
                table: "LearningProgresses",
                column: "NextReview");

            migrationBuilder.CreateIndex(
                name: "IX_LearningProgresses_UserId",
                table: "LearningProgresses",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_LearningProgresses_UserId_LastPracticed",
                table: "LearningProgresses",
                columns: new[] { "UserId", "LastPracticed" });

            migrationBuilder.CreateIndex(
                name: "IX_LearningProgresses_UserId_NextReview",
                table: "LearningProgresses",
                columns: new[] { "UserId", "NextReview" });

            migrationBuilder.CreateIndex(
                name: "IX_LearningProgresses_WordId",
                table: "LearningProgresses",
                column: "WordId");

            migrationBuilder.CreateIndex(
                name: "IX_Rules_UserId",
                table: "Rules",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_RuleSharings_RuleId",
                table: "RuleSharings",
                column: "RuleId");

            migrationBuilder.CreateIndex(
                name: "IX_RuleSharings_StudentId",
                table: "RuleSharings",
                column: "StudentId");

            migrationBuilder.CreateIndex(
                name: "IX_TrainingSessions_DictionaryId",
                table: "TrainingSessions",
                column: "DictionaryId");

            migrationBuilder.CreateIndex(
                name: "IX_TrainingSessions_UserId_StartedAt",
                table: "TrainingSessions",
                columns: new[] { "UserId", "StartedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_TranscriptionCache_WordLower",
                table: "TranscriptionCache",
                column: "WordLower",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_UserAchievements_UserId",
                table: "UserAchievements",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_UserAchievements_UserId_AchievementId",
                table: "UserAchievements",
                columns: new[] { "UserId", "AchievementId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Users_RefreshToken",
                table: "Users",
                column: "RefreshToken");

            migrationBuilder.CreateIndex(
                name: "IX_Users_RoleId",
                table: "Users",
                column: "RoleId");

            migrationBuilder.CreateIndex(
                name: "IX_Users_UserId",
                table: "Users",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_Words_DictionaryId",
                table: "Words",
                column: "DictionaryId");

            migrationBuilder.CreateIndex(
                name: "IX_Words_RelatedRuleId",
                table: "Words",
                column: "RelatedRuleId");

            migrationBuilder.CreateIndex(
                name: "IX_Words_UserId",
                table: "Words",
                column: "UserId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "Comments");

            migrationBuilder.DropTable(
                name: "DictionarySharings");

            migrationBuilder.DropTable(
                name: "Downloads");

            migrationBuilder.DropTable(
                name: "GrammarExercises");

            migrationBuilder.DropTable(
                name: "GrammarProgresses");

            migrationBuilder.DropTable(
                name: "KnowledgeTrees");

            migrationBuilder.DropTable(
                name: "LearningProgresses");

            migrationBuilder.DropTable(
                name: "RuleSharings");

            migrationBuilder.DropTable(
                name: "TrainingSessions");

            migrationBuilder.DropTable(
                name: "TranscriptionCache");

            migrationBuilder.DropTable(
                name: "UserAchievements");

            migrationBuilder.DropTable(
                name: "UserStats");

            migrationBuilder.DropTable(
                name: "TreeSkins");

            migrationBuilder.DropTable(
                name: "Words");

            migrationBuilder.DropTable(
                name: "Dictionaries");

            migrationBuilder.DropTable(
                name: "Rules");

            migrationBuilder.DropTable(
                name: "Users");

            migrationBuilder.DropTable(
                name: "Roles");
        }
    }
}
