using LearningTrainer.Context; 
using LearningTrainer.Models;
using LearningTrainer.Services;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace LearningTrainer.Services 
{
    public class LocalDataService : IDataService
    {
        private LocalDbContext _context => new LocalDbContext();


        public async Task<List<Dictionary>> GetDictionariesAsync()
        {
            using (var db = _context)
            {
                return await db.Dictionaries.Include(d => d.Words).ToListAsync();
            }
        }

        public async Task<List<Rule>> GetRulesAsync()
        {
            using (var db = _context)
            {
                return await db.Rules.ToListAsync();
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
                db.Dictionaries.Add(dictionary);
                await db.SaveChangesAsync();
                return dictionary;
            }
        }

        public async Task<Rule> AddRuleAsync(Rule rule)
        {
            using (var db = _context)
            {
                db.Rules.Add(rule);
                await db.SaveChangesAsync();
                return rule;
            }
        }

        public async Task<Word> AddWordAsync(Word word)
        {
            using (var db = _context)
            {
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
                await db.Database.ExecuteSqlRawAsync("DELETE FROM Words");
                await db.Database.ExecuteSqlRawAsync("DELETE FROM Dictionaries");

                foreach (var dict in dictionariesFromServer)
                {
                    dict.Id = 0;
                    if (dict.Words != null)
                    {
                        foreach (var word in dict.Words)
                        {
                            word.Id = 0;
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
                foreach (var rule in rulesFromServer)
                {
                    rule.Id = 0;
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

    }
}