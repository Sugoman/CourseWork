using BCrypt.Net;
using LearningTrainer.Context;
using LearningTrainerShared.Models;
using Microsoft.EntityFrameworkCore;
using System.Windows.Forms;

namespace LearningTrainer.Services
{
    public class LocalDataService : IDataService
    {
        private readonly string _userLogin;
        private int _currentLocalUserId = 0;

        public LocalDataService(string userLogin)
        {
            this._userLogin = userLogin;

            using (var db = Context)
            {
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

                    var localUser = db.Users.FirstOrDefault(u => u.Login == _userLogin);
                    if (localUser == null)
                    {
                        var safeRandomHash = BCrypt.Net.BCrypt.HashPassword(Guid.NewGuid().ToString());

                        localUser = new User
                        {
                            Login = _userLogin,
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
                    db.Database.ExecuteSqlRaw("ALTER TABLE Users ADD COLUMN Email TEXT NULL");
                }
                catch
                {
                    // Игнорируем если уже существует
                }
            }
        }
        
        private LocalDbContext Context => new LocalDbContext(_userLogin);
        public async Task<List<Dictionary>> GetDictionariesAsync()
        {
            using (var db = Context)
            {
                return await db.Dictionaries
                    .Where(d => d.UserId == _currentLocalUserId)
                    .Include(d => d.Words)
                    .ToListAsync();
            }
        }

        public async Task<List<Rule>> GetRulesAsync()
        {
            using (var db = Context)
            {
                return await db.Rules
                    .Where(r => r.UserId == _currentLocalUserId) 
                    .ToListAsync();
            }
        }

        public async Task<Dictionary> GetDictionaryByIdAsync(int id)
        {
            using (var db = Context)
            {
                return await db.Dictionaries.Include(d => d.Words)
                                 .FirstOrDefaultAsync(d => d.Id == id);
            }
        }

        public async Task<Dictionary> AddDictionaryAsync(Dictionary dictionary)
        {
            using (var db = Context)
            {
                dictionary.UserId = _currentLocalUserId;

                db.Dictionaries.Add(dictionary);
                await db.SaveChangesAsync();
                return dictionary;
            }
        }

        public async Task<Rule> AddRuleAsync(Rule rule)
        {
            using (var db = Context)
            {
                rule.UserId = _currentLocalUserId;

                db.Rules.Add(rule);
                await db.SaveChangesAsync();
                return rule;
            }
        }

        public async Task<Word> AddWordAsync(Word word)
        {
            using (var db = Context)
            {
                word.UserId = _currentLocalUserId;

                db.Words.Add(word);
                await db.SaveChangesAsync();
                return word;
            }
        }

        public async Task<bool> DeleteDictionaryAsync(int dictionaryId)
        {
            using (var db = Context)
            {
                var dict = await db.Dictionaries.FindAsync(dictionaryId);
                if (dict == null) return false;
                db.Dictionaries.Remove(dict);
                await db.SaveChangesAsync();
                return true;
            }
        }

        public async Task<bool> DeleteRuleAsync(int ruleId)
        {
            using (var db = Context)
            {
                var rule = await db.Rules.FindAsync(ruleId);
                if (rule == null) return false;
                db.Rules.Remove(rule);
                await db.SaveChangesAsync();
                return true;
            }
        }

        public async Task<bool> DeleteWordAsync(int wordId)
        {
            using (var db = Context)
            {
                var word = await db.Words.FindAsync(wordId);
                if (word == null) return false;
                db.Words.Remove(word);
                await db.SaveChangesAsync();
                return true;
            }
        }

        public async Task WipeAndStoreDictionariesAsync(List<Dictionary> dictionariesFromServer)
        {
            if (dictionariesFromServer == null) return;

            using (var db = Context)
            {
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
        }

        public async Task WipeAndStoreRulesAsync(List<Rule> rulesFromServer)
        {
            if (rulesFromServer == null) return;

            using (var db = Context)
            {
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
        }

        public async Task<List<Word>> GetReviewSessionAsync(int dictionaryId)
        {
            using (var db = Context)
            {
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
        }

        public async Task UpdateProgressAsync(UpdateProgressRequest progress)
        {
            using (var db = Context)
            {
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
    }
}
