using LearningTrainer.Context;
using LearningTrainerShared.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Collections.Generic;
using System.Security.Claims;

namespace LearningAPI.Controllers
{
    [Authorize]
    [ApiController]
    [Route("api/rules")] // /api/Rules
    public class RulesController : ControllerBase
    {
        private readonly ApiDbContext _context;

        public RulesController(ApiDbContext context)
        {
            _context = context;
        }

        [HttpGet("list/available")]
        [Authorize]
        public async Task<IActionResult> GetAvailableRules()
        {
            var userIdString = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (!int.TryParse(userIdString, out int currentUserId)) return Unauthorized();

            var sharedRuleIds = await _context.RuleSharings
                .Where(rs => rs.StudentId == currentUserId)
                .Select(rs => rs.RuleId)
                .ToListAsync();

            var rules = await _context.Rules
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
                    IsReadOnly = r.UserId != currentUserId
                })
                .ToListAsync();

            return Ok(rules);
        }

        [HttpGet]
        public async Task<IActionResult> GetRules()
        {
            var userIdString = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (!int.TryParse(userIdString, out var userId)) return Unauthorized();

            var currentUser = await _context.Users.AsNoTracking().FirstOrDefaultAsync(u => u.Id == userId);
            var teacherId = currentUser?.UserId;

            var rules = await _context.Rules
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
                    IsReadOnly = r.UserId != userId
                })
                .ToListAsync();

            return Ok(rules);
        }

        // POST: /api/Rules
        [HttpPost]
        public async Task<IActionResult> AddRule([FromBody] RuleCreateDto ruleDto)
        {
            var userIdString = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (!int.TryParse(userIdString, out var userId))
            {
                return Unauthorized();
            }

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

            _context.Rules.Add(newRule);
            await _context.SaveChangesAsync();

            return CreatedAtAction(nameof(GetRules), new { id = newRule.Id }, newRule);
        }

        // DELETE: /api/Rules/5
        [HttpDelete("{id}")]
        public async Task<IActionResult> DeleteRule(int id)
        {
            var userIdString = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (!int.TryParse(userIdString, out var userId))
            {
                return Unauthorized();
            }

            var rule = await _context.Rules.FindAsync(id);
            if (rule == null) return NotFound();

            if (rule.UserId != userId)
            {
                return Forbid();
            }

            _context.Rules.Remove(rule);
            await _context.SaveChangesAsync();

            return NoContent();
        }


        [HttpPut("{id}")]
        public async Task<IActionResult> UpdateRule(int id, [FromBody] Rule rule)
        {
            if (id != rule.Id) return BadRequest("ID mismatch");

            var userIdString = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (!int.TryParse(userIdString, out var userId)) return Unauthorized();

            var existingRule = await _context.Rules.AsNoTracking().FirstOrDefaultAsync(r => r.Id == id);
            if (existingRule == null) return NotFound();

            if (existingRule.UserId != userId) return Forbid("Вы не можете редактировать чужое правило.");

            rule.UserId = userId;
            rule.CreatedAt = existingRule.CreatedAt;

            _context.Entry(rule).State = EntityState.Modified;

            try
            {
                await _context.SaveChangesAsync();
            }
            catch (DbUpdateConcurrencyException)
            {
                if (!_context.Rules.Any(e => e.Id == id)) return NotFound();
                else throw;
            }

            return NoContent(); 
        }
    }
}