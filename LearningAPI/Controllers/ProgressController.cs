using LearningTrainer.Context;
using LearningTrainerShared.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;

namespace LearningAPI.Controllers
{
    [ApiController]
    [Route("api/progress")]
    [Authorize]
    public class ProgressController : BaseApiController
    {
        private readonly ApiDbContext _context;
        private readonly ILogger<ProgressController> _logger;

        public ProgressController(ApiDbContext context, ILogger<ProgressController> logger)
        {
            _context = context;
            _logger = logger;
        }

        // POST /api/progress/update
        [HttpPost("update")]
        public async Task<IActionResult> UpdateProgress([FromBody] UpdateProgressRequest request)
        {
            try
            {
                var userId = GetUserId();

                _logger.LogInformation("Updating progress for User={UserId}, Word={WordId}", userId, request.WordId);

                var wordExists = await _context.Words.AnyAsync(w => w.Id == request.WordId);
                if (!wordExists)
                {
                    _logger.LogWarning("Word not found: {WordId}", request.WordId);

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
                    _logger.LogInformation("New progress created for User={UserId}, Word={WordId}", userId, request.WordId);
                }
                else
                {
                    _logger.LogInformation("Progress found for update: {@Progress}", progress);
                }

                progress.LastPracticed = DateTime.UtcNow;
                progress.TotalAttempts++;

                switch (request.Quality)
                {
                    case ResponseQuality.Again: // 0
                        progress.KnowledgeLevel = 0; // Сброс уровня
                        progress.NextReview = DateTime.UtcNow.AddMinutes(5);
                        _logger.LogInformation("Progress updated to 'Again' for User={UserId}, Word={WordId}", userId, request.WordId);
                        break;

                    case ResponseQuality.Hard: // 1
                        progress.CorrectAnswers++;
                        progress.NextReview = DateTime.UtcNow.AddDays(1);
                        _logger.LogInformation("Progress updated to 'Hard' for User={UserId}, Word={WordId}", userId, request.WordId);
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
                        _logger.LogInformation("Progress updated to 'Good' for User={UserId}, Word={WordId}", userId, request.WordId);
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
                        _logger.LogInformation("Progress updated to 'Easy' for User={UserId}, Word={WordId}", userId, request.WordId);
                        break;
                }

                await _context.SaveChangesAsync();
                _logger.LogInformation("Progress successfully updated for User={UserId}, Word={WordId}", userId, request.WordId);

                return Ok(progress);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating progress for User={UserId}, Word={WordId}", GetUserId(), request.WordId);
                return StatusCode(500, "Произошла ошибка при обновлении прогресса.");
            }
        }

        // GET /api/progress/stats
        [HttpGet("stats")]
        public async Task<IActionResult> GetStats()
        {
            var userId = GetUserId();

            var progresses = _context.LearningProgresses
                .Where(p => p.UserId == userId);

            var stats = new DashboardStats();

            stats.LearnedWords = await progresses.CountAsync(p => p.KnowledgeLevel >= 4);

            var successSamples = await progresses
                .Where(p => p.TotalAttempts > 0)
                .Select(p => new { p.CorrectAnswers, p.TotalAttempts })
                .ToListAsync();

            stats.AverageSuccessRate = successSamples.Count == 0
                ? 0
                : successSamples.Average(s => (double)s.CorrectAnswers / s.TotalAttempts);

            var wordIds = await progresses
                .Select(p => p.WordId)
                .Distinct()
                .ToListAsync();

            stats.TotalWords = wordIds.Count;

            stats.TotalDictionaries = await _context.Words
                .Where(w => wordIds.Contains(w.Id))
                .Select(w => w.DictionaryId)
                .Distinct()
                .CountAsync();

            var today = DateTime.UtcNow.Date;
            var fromDate = today.AddDays(-6);

            stats.ActivityLast7Days = await progresses
                .Where(p => p.LastPracticed >= fromDate)
                .GroupBy(p => p.LastPracticed.Date)
                .Select(g => new ActivityPoint
                {
                    Date = g.Key,
                    Reviewed = g.Count(),
                    Learned = g.Count(p => p.KnowledgeLevel >= 4)
                })
                .ToListAsync();

            stats.KnowledgeDistribution = await progresses
                .GroupBy(p => p.KnowledgeLevel)
                .Select(g => new KnowledgeDistributionPoint
                {
                    Level = g.Key,
                    Count = g.Count()
                })
                .ToListAsync();

            return Ok(stats);
        }
    }
}
