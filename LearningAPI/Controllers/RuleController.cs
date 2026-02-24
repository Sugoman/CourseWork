using LearningAPI.Extensions;
using LearningTrainerShared.Context;
using LearningTrainerShared.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Distributed;
using System.Collections.Generic;
using System.Text.Json;

namespace LearningAPI.Controllers
{
    [Authorize]
    [ApiController]
    [Route("api/rules")] // /api/Rules
    public class RulesController : BaseApiController
    {
        private readonly ApiDbContext _context;
        private readonly IDistributedCache _cache;

        public RulesController(ApiDbContext context, IDistributedCache cache)
        {
            _context = context;
            _cache = cache;
        }

        [HttpGet("list/available")]
        [Authorize]
        public async Task<IActionResult> GetAvailableRules()
        {
            var currentUserId = GetUserId();

            var cacheKey = $"rules:available:{currentUserId}";
            var cached = await _cache.TryGetStringAsync(cacheKey);
            if (cached != null)
            {
                return Content(cached, "application/json");
            }

            var sharedRuleIds = await _context.RuleSharings
                .Where(rs => rs.StudentId == currentUserId)
                .Select(rs => rs.RuleId)
                .ToListAsync();

            var rules = await _context.Rules
                .IgnoreQueryFilters()
                .Include(r => r.Exercises.OrderBy(e => e.OrderIndex))
                .Where(r => r.UserId == currentUserId || sharedRuleIds.Contains(r.Id))
                .Select(r => new
                {
                    r.Id,
                    r.UserId,
                    r.Title,
                    r.MarkdownContent,
                    r.Description,
                    r.Category,
                    r.DifficultyLevel,
                    r.CreatedAt,
                    IsReadOnly = r.UserId != currentUserId,
                    Exercises = r.Exercises.OrderBy(e => e.OrderIndex).Select(e => new
                    {
                        e.Id,
                        e.Question,
                        e.OptionsJson,
                        e.CorrectIndex,
                        e.Explanation,
                        e.OrderIndex
                    }).ToList()
                })
                .ToListAsync();

            var json = JsonSerializer.Serialize(rules);
            await _cache.TrySetStringAsync(cacheKey, json, new DistributedCacheEntryOptions
            {
                AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(5)
            });

            return Ok(rules);
        }

        [HttpGet]
        public async Task<IActionResult> GetRules()
        {
            var userId = GetUserId();

            var cacheKey = $"rules:{userId}";
            var cached = await _cache.TryGetStringAsync(cacheKey);
            if (cached != null)
            {
                return Content(cached, "application/json");
            }

            var currentUser = await _context.Users.AsNoTracking().FirstOrDefaultAsync(u => u.Id == userId);
            var teacherId = currentUser?.UserId;

            var rules = await _context.Rules
                .IgnoreQueryFilters()
                .Include(r => r.Exercises.OrderBy(e => e.OrderIndex))
                .Where(r => r.UserId == userId || (teacherId != null && r.UserId == teacherId))
                .Select(r => new
                {
                    r.Id,
                    r.UserId,
                    r.Title,
                    r.MarkdownContent,
                    r.Description,
                    r.Category,
                    r.DifficultyLevel,
                    r.CreatedAt,
                    IsReadOnly = r.UserId != userId,
                    Exercises = r.Exercises.OrderBy(e => e.OrderIndex).Select(e => new
                    {
                        e.Id,
                        e.Question,
                        e.OptionsJson,
                        e.CorrectIndex,
                        e.Explanation,
                        e.OrderIndex
                    }).ToList()
                })
                .ToListAsync();

            var json = JsonSerializer.Serialize(rules);
            await _cache.TrySetStringAsync(cacheKey, json, new DistributedCacheEntryOptions
            {
                AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(5)
            });

            return Ok(rules);
        }

        // POST: /api/Rules
        [HttpPost]
        public async Task<IActionResult> AddRule([FromBody] RuleCreateDto ruleDto)
        {
            var userId = GetUserId();

            if (ruleDto == null)
            {
                return BadRequest("Rule data is required.");
            }

            if (string.IsNullOrWhiteSpace(ruleDto.Title))
            {
                return BadRequest("Title is required.");
            }

            var newRule = new Rule
            {
                Title = ruleDto.Title,
                MarkdownContent = ruleDto.MarkdownContent,
                Description = ruleDto.Description ?? "",
                Category = ruleDto.Category ?? "Grammar",
                DifficultyLevel = ruleDto.DifficultyLevel,
                CreatedAt = ruleDto.CreatedAt,
                UserId = userId
            };

            if (ruleDto.Exercises != null && ruleDto.Exercises.Count > 0)
            {
                newRule.Exercises = ruleDto.Exercises.Select((e, idx) => new GrammarExercise
                {
                    Question = e.Question,
                    Options = e.Options,
                    CorrectIndex = e.CorrectIndex,
                    Explanation = e.Explanation ?? "",
                    OrderIndex = e.OrderIndex > 0 ? e.OrderIndex : idx
                }).ToList();
            }

            _context.Rules.Add(newRule);
            await _context.SaveChangesAsync();

            await InvalidateUserRulesCacheAsync(userId);

            return CreatedAtAction(nameof(GetRules), new { id = newRule.Id }, newRule);
        }

        // DELETE: /api/Rules/5
        [HttpDelete("{id}")]
        public async Task<IActionResult> DeleteRule(int id)
        {
            var userId = GetUserId();

            var rule = await _context.Rules.FindAsync(id);
            if (rule == null) return NotFound();

            if (rule.UserId != userId)
            {
                return Forbid();
            }

            _context.Rules.Remove(rule);
            await _context.SaveChangesAsync();

            await InvalidateUserRulesCacheAsync(userId);

            return NoContent();
        }


        [HttpPut("{id}")]
        public async Task<IActionResult> UpdateRule(int id, [FromBody] Rule rule)
        {
            if (id != rule.Id) return BadRequest("ID mismatch");

            var userId = GetUserId();

            var existingRule = await _context.Rules
                .Include(r => r.Exercises)
                .FirstOrDefaultAsync(r => r.Id == id);
            if (existingRule == null) return NotFound();

            if (existingRule.UserId != userId) return Forbid("Вы не можете редактировать чужое правило.");

            existingRule.Title = rule.Title;
            existingRule.MarkdownContent = rule.MarkdownContent;
            existingRule.Description = rule.Description;
            existingRule.Category = rule.Category;
            existingRule.DifficultyLevel = rule.DifficultyLevel;
            existingRule.IsPublished = rule.IsPublished;
            existingRule.Rating = rule.Rating;
            existingRule.RatingCount = rule.RatingCount;
            existingRule.DownloadCount = rule.DownloadCount;

            // Update exercises: remove old, add new
            if (rule.Exercises != null)
            {
                _context.GrammarExercises.RemoveRange(existingRule.Exercises);
                existingRule.Exercises = rule.Exercises.Select((e, idx) => new GrammarExercise
                {
                    RuleId = id,
                    Question = e.Question,
                    OptionsJson = e.OptionsJson,
                    CorrectIndex = e.CorrectIndex,
                    Explanation = e.Explanation ?? "",
                    OrderIndex = e.OrderIndex > 0 ? e.OrderIndex : idx
                }).ToList();
            }

            try
            {
                await _context.SaveChangesAsync();
            }
            catch (DbUpdateConcurrencyException)
            {
                if (!_context.Rules.Any(e => e.Id == id)) return NotFound();
                else throw;
            }

            await InvalidateUserRulesCacheAsync(userId);

            return NoContent(); 
        }

        private async Task InvalidateUserRulesCacheAsync(int userId)
        {
            await _cache.TryRemoveAsync($"rules:{userId}");
            await _cache.TryRemoveAsync($"rules:available:{userId}");
        }
    }
}
