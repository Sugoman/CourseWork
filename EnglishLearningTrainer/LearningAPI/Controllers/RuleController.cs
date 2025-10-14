using EnglishLearningTrainer.Context;
using EnglishLearningTrainer.Models; // Убедись, что 'Rule' тут
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace LearningAPI.Controllers
{
    [ApiController]
    [Route("api/rules")] // -> /api/Rules
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
            var rules = await _context.Rules.ToListAsync();
            return Ok(rules);
        }

        // POST: /api/Rules
        [HttpPost]
        public async Task<IActionResult> AddRule([FromBody] Rule rule)
        {
            if (rule == null)
            {
                return BadRequest("Rule is required.");
            }

            _context.Rules.Add(rule);
            await _context.SaveChangesAsync();

            return CreatedAtAction(nameof(GetRules), new { id = rule.Id }, rule);
        }

        // DELETE: /api/Rules/5
        [HttpDelete("{id}")]
        public async Task<IActionResult> DeleteRule(int id)
        {
            var rule = await _context.Rules.FindAsync(id);
            if (rule == null)
            {
                return NotFound();
            }

            _context.Rules.Remove(rule);
            await _context.SaveChangesAsync();
            return NoContent();
        }
    }
}