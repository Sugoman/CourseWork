using LearningTrainer.Context;
using LearningTrainerShared.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;

namespace LearningAPI.Controllers
{
    [ApiController]
    [Route("api/progress")]
    public class ProgressController : ControllerBase
    {
        private readonly ApiDbContext _context;

        public ProgressController(ApiDbContext context)
        {
            _context = context;
        }

        // POST /api/progress/update
        [HttpPost("update")]
        public async Task<IActionResult> UpdateProgress([FromBody] UpdateProgressRequest request)
        {
            var userIdString = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (!int.TryParse(userIdString, out var userId))
            {
                return Unauthorized();
            }

            var wordExists = await _context.Words.AnyAsync(w => w.Id == request.WordId);
            if (!wordExists)
            {
                return NotFound(new { message = $"Слово с ID {request.WordId} не найдено." });
            }
            var progress = await _context.LearningProgresses
                .FirstOrDefaultAsync(p => p.UserId == userId && p.WordId == request.WordId);

            if (progress == null)
            {
                progress = new LearningProgress
                {
                    UserId = userId,
                    WordId = request.WordId,
                    KnowledgeLevel = 0,
                    NextReview = DateTime.UtcNow 
                };
                _context.LearningProgresses.Add(progress);
            }

            progress.LastPracticed = DateTime.UtcNow;
            progress.TotalAttempts++;

            switch (request.Quality)
            {
                case ResponseQuality.Again: // 0
                    progress.KnowledgeLevel = 0; // Сброс уровня
                    progress.NextReview = DateTime.UtcNow.AddMinutes(5);
                    break;

                case ResponseQuality.Hard: // 1
                    progress.CorrectAnswers++;
                    progress.NextReview = DateTime.UtcNow.AddDays(1);
                    break;

                case ResponseQuality.Good: // 2
                    progress.CorrectAnswers++;
                    if (progress.KnowledgeLevel < 5)
                        progress.KnowledgeLevel++; // +1 уровень

                    progress.NextReview = progress.KnowledgeLevel switch
                    {
                        1 => DateTime.UtcNow.AddDays(1),
                        2 => DateTime.UtcNow.AddDays(3),
                        3 => DateTime.UtcNow.AddDays(7),
                        4 => DateTime.UtcNow.AddDays(14),
                        _ => DateTime.UtcNow.AddDays(30)
                    };
                    break;

                case ResponseQuality.Easy: // 3
                    progress.CorrectAnswers++; // +2 уровня 
                    progress.KnowledgeLevel = Math.Min(5, progress.KnowledgeLevel + 2);

                    var baseIntervalDays = progress.KnowledgeLevel switch
                    {
                        1 => 1,
                        2 => 3,
                        3 => 7,
                        4 => 14,
                        _ => 30
                    };
                    progress.NextReview = DateTime.UtcNow.AddDays(baseIntervalDays * 1.5);
                    break;
            }

            await _context.SaveChangesAsync();
            return Ok(progress);
        }
    }
}