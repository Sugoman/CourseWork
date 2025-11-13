using BCrypt.Net;
using LearningTrainer.Context;
using LearningTrainerShared.Models;
using Microsoft.EntityFrameworkCore;

namespace LearningTrainer.Services
{
    public class LocalDataService : IDataService
    {
        
        private readonly string _userLogin;
        private int _currentLocalUserId = 0;

        public LocalDataService(string userLogin)
        {
            _userLogin = userLogin;

            using (var db = _context)
            {
                db.Database.EnsureCreated();

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
                            RoleId = studentRole.Id
                        };
                        db.Users.Add(localUser);
                        db.SaveChanges();
                    }

                    _currentLocalUserId = localUser.Id;
                }
            }
        }
        private LocalDbContext _context => new LocalDbContext(_userLogin);
        public async Task<List<Dictionary>> GetDictionariesAsync()
        {
            using (var db = _context)
            {
                return await db.Dictionaries
                    .Where(d => d.UserId == _currentLocalUserId)
                    .Include(d => d.Words)
                    .ToListAsync();
            }
        }

        public async Task<List<Rule>> GetRulesAsync()
        {
            using (var db = _context)
            {
                return await db.Rules
                    .Where(r => r.UserId == _currentLocalUserId) 
                    .ToListAsync();
            }
        }

        public async Task<Dictionary> GetDictionaryByIdAsync(int id)
        {
            using (var db = _context)
            {
                return await db.Dictionaries.Include(d => d.Words)
                                 .FirstOrDefaultAsync(d => d.Id == id);
            }
        }

        public async Task<Dictionary> AddDictionaryAsync(Dictionary dictionary)
        {
            using (var db = _context)
            {
                dictionary.UserId = _currentLocalUserId;

                db.Dictionaries.Add(dictionary);
                await db.SaveChangesAsync();
                return dictionary;
            }
        }

        public async Task<Rule> AddRuleAsync(Rule rule)
        {
            using (var db = _context)
            {
                rule.UserId = _currentLocalUserId;

                db.Rules.Add(rule);
                await db.SaveChangesAsync();
                return rule;
            }
        }

        public async Task<Word> AddWordAsync(Word word)
        {
            using (var db = _context)
            {
                word.UserId = _currentLocalUserId;

                db.Words.Add(word);
                await db.SaveChangesAsync();
                return word;
            }
        }

        public async Task<bool> DeleteDictionaryAsync(int dictionaryId)
        {
            using (var db = _context)
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
            using (var db = _context)
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
            using (var db = _context)
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
            using (var db = _context)
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
            using (var db = _context)
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
            using (var db = _context)
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
            using (var db = _context)
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
    }
}