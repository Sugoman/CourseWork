using BCrypt.Net;
using LearningTrainerShared.Context;
using LearningTrainerShared.Models;
using Microsoft.EntityFrameworkCore;
using System.Windows.Forms;

namespace LearningTrainer.Services
{
    public class LocalDataService : IDataService
    {
        private readonly string _userLogin;
        private int _currentLocalUserId = 0;
        private LocalDbContext _cachedContext;

        public LocalDataService(string userLogin)
        {
            this._userLogin = userLogin;

            var db = Context;
            // Обеспечиваем актуальную схему без удаления пользовательских данных при каждом запуске
            db.Database.EnsureCreated();

            // Проверяем и добавляем колонку Email если её нет
            EnsureEmailColumnExists(db);

            if (!string.IsNullOrEmpty(_userLogin))
            {
                var studentRole = db.Roles.FirstOrDefault(r => r.Id == 3);
                if (studentRole == null)
                {
                    studentRole = new Role { Id = 3, Name = "Student" };
                    db.Roles.Add(studentRole);
                    db.SaveChanges();
                }

                var localUser = db.Users.FirstOrDefault(u => u.Username != null && u.Username == _userLogin);
                if (localUser == null)
                {
                    var safeRandomHash = BCrypt.Net.BCrypt.HashPassword(Guid.NewGuid().ToString());

                    localUser = new User
                    {
                        Username = _userLogin,
                        Email = "", // Локальный пользователь без email
                        PasswordHash = safeRandomHash,
                        RoleId = studentRole.Id,
                        RefreshToken = null,
                        RefreshTokenExpiryTime = null,
                        IsRefreshTokenRevoked = false
                    };
                    db.Users.Add(localUser);
                    db.SaveChanges();
                }

                _currentLocalUserId = localUser.Id;
            }
        }
        
        private void EnsureEmailColumnExists(LocalDbContext db)
        {
            try
            {
                // Проверяем, есть ли колонка Email
                db.Database.ExecuteSqlRaw("SELECT Email FROM Users LIMIT 1");
            }
            catch
            {
                // Колонки нет, добавляем
                try
                {
                    db.Database.ExecuteSqlRaw("ALTER TABLE Users ADD COLUMN Email TEXT NOT NULL DEFAULT ''");
                }
                catch
                {
                    // Игнорируем если уже существует
                }
            }

            // Обновляем NULL значения Email на пустую строку
            try
            {
                db.Database.ExecuteSqlRaw("UPDATE Users SET Email = '' WHERE Email IS NULL");
            }
            catch { }

            try
            {
                // Проверяем, есть ли колонка Username
                db.Database.ExecuteSqlRaw("SELECT Username FROM Users LIMIT 1");
            }
            catch
            {
                // Колонки нет, добавляем
                try
                {
                    db.Database.ExecuteSqlRaw("ALTER TABLE Users ADD COLUMN Username TEXT NOT NULL DEFAULT ''");
                    db.Database.ExecuteSqlRaw("UPDATE Users SET Username = Login WHERE Login IS NOT NULL");
                }
                catch
                {
                    // Игнорируем если уже существует
                }
            }

            // Обновляем NULL значения Username на пустую строку
            try
            {
                db.Database.ExecuteSqlRaw("UPDATE Users SET Username = '' WHERE Username IS NULL");
            }
            catch { }
        }
        
        private LocalDbContext Context
        {
            get
            {
                _cachedContext ??= new LocalDbContext(_userLogin);
                return _cachedContext;
            }
        }
        public async Task<List<Dictionary>> GetDictionariesAsync()
        {
            var db = Context;
            return await db.Dictionaries
                .Where(d => d.UserId == _currentLocalUserId)
                .Include(d => d.Words)
                .ToListAsync();
        }

        public async Task<List<Rule>> GetRulesAsync()
        {
            var db = Context;
            return await db.Rules
                .Where(r => r.UserId == _currentLocalUserId) 
                .ToListAsync();
        }

        public async Task<Dictionary> GetDictionaryByIdAsync(int id)
        {
            var db = Context;
            return await db.Dictionaries.Include(d => d.Words)
                             .FirstOrDefaultAsync(d => d.Id == id);
        }

        public async Task<Dictionary> AddDictionaryAsync(Dictionary dictionary)
        {
            var db = Context;
            dictionary.UserId = _currentLocalUserId;

            db.Dictionaries.Add(dictionary);
            await db.SaveChangesAsync();
            return dictionary;
        }

        public async Task<Rule> AddRuleAsync(Rule rule)
        {
            var db = Context;
            rule.UserId = _currentLocalUserId;

            db.Rules.Add(rule);
            await db.SaveChangesAsync();
            return rule;
        }

        public async Task<Word> AddWordAsync(Word word)
        {
            var db = Context;
            word.UserId = _currentLocalUserId;

            db.Words.Add(word);
            await db.SaveChangesAsync();
            return word;
        }

        public async Task<bool> DeleteDictionaryAsync(int dictionaryId)
        {
            var db = Context;
            var dict = await db.Dictionaries.FindAsync(dictionaryId);
            if (dict == null) return false;
            db.Dictionaries.Remove(dict);
            await db.SaveChangesAsync();
            return true;
        }

        public async Task<bool> DeleteRuleAsync(int ruleId)
        {
            var db = Context;
            var rule = await db.Rules.FindAsync(ruleId);
            if (rule == null) return false;
            db.Rules.Remove(rule);
            await db.SaveChangesAsync();
            return true;
        }

        public async Task<bool> DeleteWordAsync(int wordId)
        {
            var db = Context;
            var word = await db.Words.FindAsync(wordId);
            if (word == null) return false;
            db.Words.Remove(word);
            await db.SaveChangesAsync();
            return true;
        }

        public async Task WipeAndStoreDictionariesAsync(List<Dictionary> dictionariesFromServer)
        {
            if (dictionariesFromServer == null) return;

            var db = Context;
            await db.Database.ExecuteSqlRawAsync("DELETE FROM LearningProgresses");
            await db.Database.ExecuteSqlRawAsync("DELETE FROM Words");
            await db.Database.ExecuteSqlRawAsync("DELETE FROM Dictionaries");

            if (_currentLocalUserId == 0) return;

            foreach (var dict in dictionariesFromServer)
            {
                db.Entry(dict).State = EntityState.Detached;
                dict.User = null;

                dict.Id = 0;
                dict.UserId = _currentLocalUserId;

                if (dict.Words != null)
                {
                    foreach (var word in dict.Words)
                    {
                        db.Entry(word).State = EntityState.Detached;
                        word.User = null;
                        word.Dictionary = null;

                        word.Id = 0;
                        word.DictionaryId = 0;
                        word.UserId = _currentLocalUserId;
                    }
                }
            }

            await db.Dictionaries.AddRangeAsync(dictionariesFromServer);
            await db.SaveChangesAsync();
        }

        public async Task WipeAndStoreRulesAsync(List<Rule> rulesFromServer)
        {
            if (rulesFromServer == null) return;

            var db = Context;
            await db.Database.ExecuteSqlRawAsync("DELETE FROM Rules");

            if (_currentLocalUserId == 0) return;

            foreach (var rule in rulesFromServer)
            {
                db.Entry(rule).State = EntityState.Detached;
                rule.User = null;

                rule.Id = 0;
                rule.UserId = _currentLocalUserId;
            }

            await db.Rules.AddRangeAsync(rulesFromServer);
            await db.SaveChangesAsync();
        }

        public Task<bool> UpdateDictionaryAsync(Dictionary dictionary)
        {
            throw new NotImplementedException();
        }

        public Task<List<Word>> GetWordsByDictionaryAsync(int dictionaryId)
        {
            throw new NotImplementedException();
        }

        public Task InitializeTestDataAsync()
        {
            return Task.CompletedTask;
        }
        public void SetToken(string accessToken)
        {
            throw new NotImplementedException();
        }

        public void Dispose()
        {
            _cachedContext?.Dispose();
            _cachedContext = null;
        }

        public async Task<List<Word>> GetReviewSessionAsync(int dictionaryId)
        {
            var db = Context;
            var now = DateTime.UtcNow;

            var allWordsAndDates = await db.Words
                .Where(w => w.DictionaryId == dictionaryId && w.UserId == _currentLocalUserId)
                .Select(w => new
                {
                    TheWord = w,
                    ReviewDate = w.Progress
                        .Where(p => p.UserId == _currentLocalUserId)
                        .Select(p => (DateTime?)p.NextReview)
                        .FirstOrDefault()
                })
                .ToListAsync();

            var studySession = allWordsAndDates
                .Where(x => !x.ReviewDate.HasValue || x.ReviewDate.Value <= now)
                .Select(x => x.TheWord)
                .ToList();

            var random = new Random();
            return studySession.OrderBy(w => random.Next()).ToList();
        }

        public async Task UpdateProgressAsync(UpdateProgressRequest progress)
        {
            var db = Context;
            var existingProgress = await db.LearningProgresses
                .FirstOrDefaultAsync(p => p.UserId == _currentLocalUserId && p.WordId == progress.WordId);

            if (existingProgress == null)
            {
                existingProgress = new LearningProgress
                {
                    UserId = _currentLocalUserId,
                    WordId = progress.WordId,
                    KnowledgeLevel = 0,
                    NextReview = DateTime.UtcNow
                };
                db.LearningProgresses.Add(existingProgress);
            }

            existingProgress.LastPracticed = DateTime.UtcNow;
            existingProgress.TotalAttempts++;

            switch (progress.Quality)
            {
                case ResponseQuality.Again:
                    existingProgress.KnowledgeLevel = 0;
                    existingProgress.NextReview = DateTime.UtcNow.AddMinutes(5);
                    break;
                case ResponseQuality.Hard:
                    existingProgress.CorrectAnswers++;
                    existingProgress.NextReview = DateTime.UtcNow.AddDays(1);
                    break;
                case ResponseQuality.Good:
                    existingProgress.CorrectAnswers++;
                    if (existingProgress.KnowledgeLevel < 5)
                        existingProgress.KnowledgeLevel++;

                    existingProgress.NextReview = existingProgress.KnowledgeLevel switch
                    {
                        1 => DateTime.UtcNow.AddDays(1),
                        2 => DateTime.UtcNow.AddDays(3),
                        3 => DateTime.UtcNow.AddDays(7),
                        4 => DateTime.UtcNow.AddDays(14),
                        _ => DateTime.UtcNow.AddDays(30)
                    };
                    break;
                case ResponseQuality.Easy:
                    existingProgress.CorrectAnswers++;
                    existingProgress.KnowledgeLevel = Math.Min(5, existingProgress.KnowledgeLevel + 2);

                    var baseIntervalDays = existingProgress.KnowledgeLevel switch
                    {
                        1 => 1,
                        2 => 3,
                        3 => 7,
                        4 => 14,
                        _ => 30
                    };
                    existingProgress.NextReview = DateTime.UtcNow.AddDays(baseIntervalDays * 1.5);
                    break;
            }

            await db.SaveChangesAsync();
        }

        public Task<string> ChangePasswordAsync(ChangePasswordRequest request)
        {
            return Task.FromResult("Смена пароля в оффлайн-режиме невозможна.");
        }

        public Task<string> RegisterAsync(RegisterRequest request)
        {
            throw new NotImplementedException();
        }

        public Task<UserSessionDto> LoginAsync(object loginRequest)
        {
            throw new NotImplementedException();
        }

        public Task<UpgradeResultDto> UpgradeToTeacherAsync()
        {
            MessageBox.Show("Вы находитесь в Офлайн режиме");
            return null;
        }

        public Task<List<StudentDto>> GetMyStudentsAsync()
        {
            throw new NotImplementedException();
        }

        public Task<List<int>> GetDictionarySharingStatusAsync(int dictionaryId)
        {
            throw new NotImplementedException();
        }

        public Task<SharingResultDto> ToggleDictionarySharingAsync(int dictionaryId, int studentId)
        {
            throw new NotImplementedException();
        }

        public Task<List<Dictionary>> GetAvailableDictionariesAsync()
        {
            throw new NotImplementedException();
        }

        public Task<List<Rule>> GetAvailableRulesAsync()
        {
            throw new NotImplementedException();
        }

        public Task<List<int>> GetRuleSharingStatusAsync(int ruleId)
        {
            throw new NotImplementedException();
        }

        public Task<SharingResultDto> ToggleRuleSharingAsync(int ruleId, int studentId)
        {
            throw new NotImplementedException();
        }

        public Task<bool> UpdateRuleAsync(Rule rule)
        {
            throw new NotImplementedException();
        }

        public Task<DashboardStats> GetStatsAsync()
        {
            throw new NotImplementedException();
        }
        
        // Marketplace - недоступно в оффлайн режиме
        public Task<bool> PublishDictionaryAsync(int dictionaryId)
        {
            MessageBox.Show("Публикация в маркетплейс недоступна в оффлайн-режиме", "Офлайн режим", MessageBoxButtons.OK, MessageBoxIcon.Information);
            return Task.FromResult(false);
        }

        public Task<bool> UnpublishDictionaryAsync(int dictionaryId)
        {
            MessageBox.Show("Управление публикацией недоступно в оффлайн-режиме", "Офлайн режим", MessageBoxButtons.OK, MessageBoxIcon.Information);
            return Task.FromResult(false);
        }

        public Task<bool> PublishRuleAsync(int ruleId)
        {
            MessageBox.Show("Публикация в маркетплейс недоступна в оффлайн-режиме", "Офлайн режим", MessageBoxButtons.OK, MessageBoxIcon.Information);
            return Task.FromResult(false);
        }

        public Task<bool> UnpublishRuleAsync(int ruleId)
        {
            MessageBox.Show("Управление публикацией недоступно в оффлайн-режиме", "Офлайн режим", MessageBoxButtons.OK, MessageBoxIcon.Information);
            return Task.FromResult(false);
        }

        // Export - в оффлайн-режиме используем локальную сериализацию
        public Task<byte[]> ExportDictionaryAsJsonAsync(int dictionaryId)
        {
            var db = Context;
            var dictionary = db.Dictionaries
                .Include(d => d.Words)
                .FirstOrDefault(d => d.Id == dictionaryId);

            if (dictionary == null)
                throw new InvalidOperationException("Словарь не найден");

            var exportData = new
            {
                Name = dictionary.Name,
                Description = dictionary.Description,
                LanguageFrom = dictionary.LanguageFrom,
                LanguageTo = dictionary.LanguageTo,
                ExportDate = DateTime.UtcNow,
                Words = dictionary.Words.Select(w => new
                {
                    Original = w.OriginalWord,
                    Translation = w.Translation,
                    PartOfSpeech = w.Transcription,
                    Example = w.Example
                }).ToList()
            };

            var json = System.Text.Json.JsonSerializer.Serialize(exportData, new System.Text.Json.JsonSerializerOptions 
            { 
                WriteIndented = true,
                Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping
            });
            return Task.FromResult(System.Text.Encoding.UTF8.GetBytes(json));
        }

        public Task<byte[]> ExportDictionaryAsCsvAsync(int dictionaryId)
        {
            var db = Context;
            var dictionary = db.Dictionaries
                .Include(d => d.Words)
                .FirstOrDefault(d => d.Id == dictionaryId);

            if (dictionary == null)
                throw new InvalidOperationException("Словарь не найден");

            var sb = new System.Text.StringBuilder();
            sb.AppendLine("Original,Translation,Part of Speech,Example");

            foreach (var word in dictionary.Words)
            {
                sb.AppendLine($"\"{word.OriginalWord}\",\"{word.Translation}\",\"{word.Transcription ?? ""}\",\"{word.Example ?? ""}\"");
            }

            return Task.FromResult(System.Text.Encoding.UTF8.GetBytes(sb.ToString()));
        }

        public async Task<byte[]> ExportAllDictionariesAsZipAsync()
        {
            var db = Context;
            var dictionaries = db.Dictionaries
                .Include(d => d.Words)
                .Where(d => d.UserId == _currentLocalUserId)
                .ToList();

            if (!dictionaries.Any())
                throw new InvalidOperationException("Нет словарей для экспорта");

            using var memoryStream = new System.IO.MemoryStream();
            using (var archive = new System.IO.Compression.ZipArchive(memoryStream, System.IO.Compression.ZipArchiveMode.Create, true))
            {
                foreach (var dictionary in dictionaries)
                {
                    var exportData = new
                    {
                        Name = dictionary.Name,
                        Description = dictionary.Description,
                        LanguageFrom = dictionary.LanguageFrom,
                        LanguageTo = dictionary.LanguageTo,
                        ExportDate = DateTime.UtcNow,
                        Words = dictionary.Words.Select(w => new
                        {
                            Original = w.OriginalWord,
                            Translation = w.Translation,
                            PartOfSpeech = w.Transcription,
                            Example = w.Example
                        }).ToList()
                    };

                    var json = System.Text.Json.JsonSerializer.Serialize(exportData, new System.Text.Json.JsonSerializerOptions 
                    { 
                        WriteIndented = true,
                        Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping
                    });

                    var entry = archive.CreateEntry($"{dictionary.Name}.json");
                    using var entryStream = entry.Open();
                    await entryStream.WriteAsync(System.Text.Encoding.UTF8.GetBytes(json));
                }
            }

            return memoryStream.ToArray();
        }

        #region Marketplace - недоступно в оффлайн режиме

        public Task<PagedResult<MarketplaceDictionaryItem>> GetPublicDictionariesAsync(string? search, string? languageFrom, string? languageTo, int page, int pageSize)
        {
            MessageBox.Show("Маркетплейс недоступен в оффлайн-режиме", "Офлайн режим", MessageBoxButtons.OK, MessageBoxIcon.Information);
            return Task.FromResult(new PagedResult<MarketplaceDictionaryItem>());
        }

        public Task<PagedResult<MarketplaceRuleItem>> GetPublicRulesAsync(string? search, string? category, int difficulty, int page, int pageSize)
        {
            MessageBox.Show("Маркетплейс недоступен в оффлайн-режиме", "Офлайн режим", MessageBoxButtons.OK, MessageBoxIcon.Information);
            return Task.FromResult(new PagedResult<MarketplaceRuleItem>());
        }

        public Task<MarketplaceDictionaryDetails?> GetMarketplaceDictionaryDetailsAsync(int id)
        {
            return Task.FromResult<MarketplaceDictionaryDetails?>(null);
        }

        public Task<MarketplaceRuleDetails?> GetMarketplaceRuleDetailsAsync(int id)
        {
            return Task.FromResult<MarketplaceRuleDetails?>(null);
        }

        public Task<List<MarketplaceRuleItem>> GetRelatedRulesAsync(int ruleId, string category)
        {
            return Task.FromResult(new List<MarketplaceRuleItem>());
        }

        public Task<List<CommentItem>> GetDictionaryCommentsAsync(int id)
        {
            return Task.FromResult(new List<CommentItem>());
        }

        public Task<List<CommentItem>> GetRuleCommentsAsync(int id)
        {
            return Task.FromResult(new List<CommentItem>());
        }

        public Task<bool> AddDictionaryCommentAsync(int dictionaryId, int rating, string text)
        {
            MessageBox.Show("Комментарии недоступны в оффлайн-режиме", "Офлайн режим", MessageBoxButtons.OK, MessageBoxIcon.Information);
            return Task.FromResult(false);
        }

        public Task<bool> AddRuleCommentAsync(int ruleId, int rating, string text)
        {
            MessageBox.Show("Комментарии недоступны в оффлайн-режиме", "Офлайн режим", MessageBoxButtons.OK, MessageBoxIcon.Information);
            return Task.FromResult(false);
        }

        public Task<bool> HasUserReviewedDictionaryAsync(int dictionaryId)
        {
            return Task.FromResult(false);
        }

        public Task<bool> HasUserReviewedRuleAsync(int ruleId)
        {
            return Task.FromResult(false);
        }

        public Task<(bool Success, string Message, int? NewId)> DownloadDictionaryFromMarketplaceAsync(int dictionaryId)
        {
            MessageBox.Show("Скачивание из маркетплейса недоступно в оффлайн-режиме", "Офлайн режим", MessageBoxButtons.OK, MessageBoxIcon.Information);
            return Task.FromResult((false, "Офлайн режим", (int?)null));
        }

        public Task<(bool Success, string Message, int? NewId)> DownloadRuleFromMarketplaceAsync(int ruleId)
        {
            MessageBox.Show("Скачивание из маркетплейса недоступно в оффлайн-режиме", "Офлайн режим", MessageBoxButtons.OK, MessageBoxIcon.Information);
            return Task.FromResult((false, "Офлайн режим", (int?)null));
        }

        public Task<List<DownloadedItem>> GetDownloadedContentAsync()
        {
            return Task.FromResult(new List<DownloadedItem>());
        }

        public Task<DailyPlanDto?> GetDailyPlanAsync(int newWordsLimit = 10, int reviewLimit = 20)
        {
            return Task.FromResult<DailyPlanDto?>(null);
        }

        public Task<List<TrainingWordDto>> GetTrainingWordsAsync(string mode, int? dictionaryId = null, int limit = 20)
        {
            return Task.FromResult(new List<TrainingWordDto>());
        }

        public Task<StarterPackResult?> InstallStarterPackAsync()
        {
            MessageBox.Show("Стартовый набор недоступен в оффлайн-режиме", "Офлайн режим", MessageBoxButtons.OK, MessageBoxIcon.Information);
            return Task.FromResult<StarterPackResult?>(null);
        }

        public Task<LearningTrainerShared.Models.Statistics.UserStatistics?> GetStatisticsAsync(string period = "week")
        {
            // В оффлайн-режиме возвращаем базовую статистику из локальных данных
            var stats = new LearningTrainerShared.Models.Statistics.UserStatistics
            {
                TotalWords = 0,
                LearnedWords = 0,
                CurrentStreak = 0,
                BestStreak = 0
            };
            return Task.FromResult<LearningTrainerShared.Models.Statistics.UserStatistics?>(stats);
        }

        public Task SaveTrainingSessionAsync(DateTime startedAt, DateTime completedAt, int wordsReviewed, int correctAnswers, int wrongAnswers, string mode, int? dictionaryId)
        {
            // В оффлайн-режиме не сохраняем сессии на сервер
            return Task.CompletedTask;
        }

        #endregion
    }
}
