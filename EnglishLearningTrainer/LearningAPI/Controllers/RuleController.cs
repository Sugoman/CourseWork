using LearningTrainer.Context;
using LearningTrainerShared.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Collections.Generic;
using System.Security.Claims;

namespace LearningAPI.Controllers
{
    [ApiController]
    [Route("api/rules")] // /api/Rules
    public class RulesController : ControllerBase
    {
        private readonly ApiDbContext _context;

        public RulesController(ApiDbContext context)
        {
            _context = context;
        }

        // GET: /api/Rules
        [HttpGet]
        public async Task<IActionResult> GetRules()
        {
            var userIdString = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (!int.TryParse(userIdString, out var userId))
            {
                return Unauthorized();
            }

            var rules = await _context.Rules
                .Where(x => x.UserId == userId)
                .Select(r => new
                {
                    r.Id,
                    r.UserId,
                    r.Title,
                    r.MarkdownContent,
                    r.Description,
                    r.Category,
                    r.DifficultyLevel,
                    r.CreatedAt
                })
                .ToListAsync();
            return Ok(rules);
        }

        // POST: /api/Rules
        [HttpPost]
        public async Task<IActionResult> AddRule([FromBody] Rule rule)
        {
            var userIdString = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (!int.TryParse(userIdString, out var userId))
            {
                return Unauthorized();
            }

            if (rule == null)
            {
                return BadRequest("Rule is required.");
            }
            var newRule = new Rule
            {
                Title = rule.Title,
                MarkdownContent = rule.MarkdownContent,
                Description = rule.Description,
                Category = rule.Category,
                DifficultyLevel = rule.DifficultyLevel,
                CreatedAt = rule.CreatedAt,
                UserId = userId
            };

            _context.Rules.Add(newRule);
            await _context.SaveChangesAsync();

            return CreatedAtAction(nameof(GetRules), new { id = rule.Id }, rule);
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
    }
}