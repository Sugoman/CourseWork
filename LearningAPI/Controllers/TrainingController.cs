using LearningTrainerShared.Context;
using LearningTrainerShared.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace LearningAPI.Controllers;

/// <summary>
/// Контроллер для управления тренировками
/// </summary>
[ApiController]
[Route("api/training")]
[Authorize]
public class TrainingController : BaseApiController
{
    private readonly ApiDbContext _context;
    private readonly ILogger<TrainingController> _logger;
    
    // Настройки по умолчанию для количества слов
    private const int DefaultNewWordsLimit = 10;
    private const int DefaultReviewLimit = 20;

    public TrainingController(ApiDbContext context, ILogger<TrainingController> logger)
    {
        _context = context;
        _logger = logger;
    }

    /// <summary>
    /// Получить план тренировки на сегодня
    /// </summary>
    [HttpGet("daily-plan")]
    public async Task<ActionResult<DailyPlanDto>> GetDailyPlan(
        [FromQuery] int newWordsLimit = DefaultNewWordsLimit,
        [FromQuery] int reviewLimit = DefaultReviewLimit)
    {
        var userId = GetUserId();
        var now = DateTime.UtcNow;
        var today = now.Date;

        _logger.LogInformation("Getting daily plan for User={UserId}", userId);

        try
        {
            // Получаем ID слов пользователя
            var userWordIds = await _context.Words
                .Where(w => w.UserId == userId)
                .Select(w => w.Id)
                .ToListAsync();

            // ID слов с прогрессом (серверная фильтрация)
            var progressWordIds = await _context.LearningProgresses
                .Where(p => p.UserId == userId)
                .Select(p => p.WordId)
                .ToListAsync();

            var progressWordIdSet = progressWordIds.ToHashSet();

            // Слова к повторению (серверная фильтрация + проекция)
            var reviewWords = await _context.LearningProgresses
                .Include(p => p.Word)
                    .ThenInclude(w => w.Dictionary)
                .Where(p => p.UserId == userId && p.NextReview <= now && p.Word != null)
                .OrderBy(p => p.NextReview)
                .Take(reviewLimit)
                .Select(p => MapToTrainingWordProjection(p))
                .ToListAsync();

            // Сложные слова (серверная фильтрация + проекция)
            var difficultWords = await _context.LearningProgresses
                .Include(p => p.Word)
                    .ThenInclude(w => w.Dictionary)
                .Where(p => p.UserId == userId && p.Word != null &&
                           (p.KnowledgeLevel == 0 ||
                            (p.TotalAttempts > 2 && p.CorrectAnswers < p.TotalAttempts / 2)))
                .OrderBy(p => p.KnowledgeLevel)
                .Take(10)
                .Select(p => MapToTrainingWordProjection(p))
                .ToListAsync();

            // Новые слова (без прогресса)
            var newWords = await _context.Words
                .Where(w => w.UserId == userId && !progressWordIdSet.Contains(w.Id))
                .Include(w => w.Dictionary)
                .OrderBy(w => w.AddedAt)
                .Take(newWordsLimit)
                .Select(w => new TrainingWordDto
                {
                    WordId = w.Id,
                    OriginalWord = w.OriginalWord,
                    Translation = w.Translation,
                    Transcription = w.Transcription,
                    Example = w.Example,
                    DictionaryName = w.Dictionary != null ? w.Dictionary.Name : "",
                    DictionaryId = w.DictionaryId,
                    KnowledgeLevel = 0,
                    NextReview = null,
                    TotalAttempts = 0,
                    CorrectAnswers = 0
                })
                .ToListAsync();

            // Статистика (серверная агрегация)
            var totalReviewCount = await _context.LearningProgresses
                .CountAsync(p => p.UserId == userId && p.NextReview <= now);

            var completedToday = await _context.LearningProgresses
                .CountAsync(p => p.UserId == userId && p.LastPracticed >= today);

            var lastPractice = await _context.LearningProgresses
                .Where(p => p.UserId == userId && p.LastPracticed != default)
                .OrderByDescending(p => p.LastPracticed)
                .Select(p => (DateTime?)p.LastPracticed)
                .FirstOrDefaultAsync();

            // Подсчёт streak (серии дней подряд)
            var streak = await CalculateStreakAsync(userId, today);

            var plan = new DailyPlanDto
            {
                ReviewWords = reviewWords,
                NewWords = newWords,
                DifficultWords = difficultWords,
                Stats = new DailyPlanStats
                {
                    TotalReviewCount = totalReviewCount,
                    TotalNewCount = userWordIds.Count - progressWordIdSet.Count,
                    TotalDifficultCount = difficultWords.Count,
                    CompletedToday = completedToday,
                    CurrentStreak = streak,
                    LastPracticeDate = lastPractice
                }
            };

            _logger.LogInformation(
                "Daily plan for User={UserId}: Review={ReviewCount}, New={NewCount}, Difficult={DifficultCount}",
                userId, plan.Stats.TotalReviewCount, plan.Stats.TotalNewCount, plan.Stats.TotalDifficultCount);

            return Ok(plan);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting daily plan for User={UserId}", userId);
            return StatusCode(500, "Произошла ошибка при получении плана тренировки.");
        }
    }

    /// <summary>
    /// Получить слова для тренировки (смешанный режим)
    /// </summary>
    [HttpGet("words")]
    public async Task<ActionResult<List<TrainingWordDto>>> GetTrainingWords(
        [FromQuery] string mode = "mixed",
        [FromQuery] int? dictionaryId = null,
        [FromQuery] int limit = 20)
    {
        var userId = GetUserId();
        var now = DateTime.UtcNow;

        try
        {
            IQueryable<LearningProgress> query = _context.LearningProgresses
                .Where(p => p.UserId == userId)
                .Include(p => p.Word)
                    .ThenInclude(w => w.Dictionary);

            if (dictionaryId.HasValue)
            {
                query = query.Where(p => p.Word.DictionaryId == dictionaryId.Value);
            }

            List<TrainingWordDto> result;

            switch (mode.ToLower())
            {
                case "review":
                    // Только слова к повторению
                    result = await query
                        .Where(p => p.NextReview <= now)
                        .OrderBy(p => p.NextReview)
                        .Take(limit)
                        .Select(p => MapToTrainingWordProjection(p))
                        .ToListAsync();
                    break;

                case "difficult":
                    // Только сложные слова
                    result = await query
                        .Where(p => p.KnowledgeLevel == 0 || 
                                   (p.TotalAttempts > 2 && p.CorrectAnswers < p.TotalAttempts / 2))
                        .OrderBy(p => p.KnowledgeLevel)
                        .Take(limit)
                        .Select(p => MapToTrainingWordProjection(p))
                        .ToListAsync();
                    break;

                case "new":
                    // Только новые слова
                    var progressedWordIds = await _context.LearningProgresses
                        .Where(p => p.UserId == userId)
                        .Select(p => p.WordId)
                        .ToListAsync();

                    IQueryable<Word> newWordsQuery = _context.Words
                        .Where(w => w.UserId == userId && !progressedWordIds.Contains(w.Id))
                        .Include(w => w.Dictionary);

                    if (dictionaryId.HasValue)
                    {
                        newWordsQuery = newWordsQuery.Where(w => w.DictionaryId == dictionaryId.Value);
                    }

                    result = await newWordsQuery
                        .Take(limit)
                        .Select(w => new TrainingWordDto
                        {
                            WordId = w.Id,
                            OriginalWord = w.OriginalWord,
                            Translation = w.Translation,
                            Transcription = w.Transcription,
                            Example = w.Example,
                            DictionaryName = w.Dictionary != null ? w.Dictionary.Name : "",
                            DictionaryId = w.DictionaryId,
                            KnowledgeLevel = 0,
                            TotalAttempts = 0,
                            CorrectAnswers = 0
                        })
                        .ToListAsync();
                    break;

                case "mixed":
                default:
                    // Смешанный режим: повторение + новые
                    var reviewPart = limit / 2;
                    var newPart = limit - reviewPart;

                    var reviewItems = await query
                        .Where(p => p.NextReview <= now)
                        .OrderBy(p => p.NextReview)
                        .Take(reviewPart)
                        .Select(p => MapToTrainingWordProjection(p))
                        .ToListAsync();

                    var existingWordIds = await _context.LearningProgresses
                        .Where(p => p.UserId == userId)
                        .Select(p => p.WordId)
                        .ToListAsync();

                    IQueryable<Word> newWordsForMixed = _context.Words
                        .Where(w => w.UserId == userId && !existingWordIds.Contains(w.Id))
                        .Include(w => w.Dictionary);

                    if (dictionaryId.HasValue)
                    {
                        newWordsForMixed = newWordsForMixed.Where(w => w.DictionaryId == dictionaryId.Value);
                    }

                    var newItems = await newWordsForMixed
                        .Take(newPart)
                        .Select(w => new TrainingWordDto
                        {
                            WordId = w.Id,
                            OriginalWord = w.OriginalWord,
                            Translation = w.Translation,
                            Transcription = w.Transcription,
                            Example = w.Example,
                            DictionaryName = w.Dictionary != null ? w.Dictionary.Name : "",
                            DictionaryId = w.DictionaryId,
                            KnowledgeLevel = 0,
                            TotalAttempts = 0,
                            CorrectAnswers = 0
                        })
                        .ToListAsync();

                    result = reviewItems.Concat(newItems).ToList();
                    break;
            }

            // Перемешиваем для разнообразия
            var random = new Random();
            result = result.OrderBy(_ => random.Next()).ToList();

            return Ok(result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting training words for User={UserId}", userId);
            return StatusCode(500, "Произошла ошибка при получении слов для тренировки.");
        }
    }

    /// <summary>
    /// Получить стартовый набор контента для новичков
    /// </summary>
    [HttpPost("starter-pack")]
    public async Task<ActionResult> GetStarterPack()
    {
        var userId = GetUserId();

        try
        {
            // Проверяем, есть ли уже контент у пользователя
            var hasContent = await _context.Dictionaries
                .AnyAsync(d => d.UserId == userId);

            if (hasContent)
            {
                return BadRequest(new { message = "У вас уже есть словари. Стартовый набор доступен только для новых пользователей." });
            }

            // Создаём демо-словарь с базовыми словами
            var starterDictionary = new Dictionary
            {
                UserId = userId,
                Name = "Базовые слова",
                Description = "Стартовый набор из 20 самых употребляемых английских слов",
                LanguageFrom = "English",
                LanguageTo = "Russian",
                Words = new List<Word>()
            };

            var starterWords = new List<(string orig, string trans, string example)>
            {
                ("hello", "привет", "Hello, how are you?"),
                ("goodbye", "до свидания", "Goodbye, see you later!"),
                ("thank you", "спасибо", "Thank you for your help!"),
                ("please", "пожалуйста", "Please, come in."),
                ("yes", "да", "Yes, I agree."),
                ("no", "нет", "No, thank you."),
                ("sorry", "извините", "Sorry, I'm late."),
                ("help", "помощь", "Can you help me?"),
                ("water", "вода", "I need some water."),
                ("food", "еда", "The food is delicious."),
                ("time", "время", "What time is it?"),
                ("today", "сегодня", "Today is a good day."),
                ("tomorrow", "завтра", "See you tomorrow!"),
                ("good", "хороший", "This is a good book."),
                ("bad", "плохой", "Bad weather today."),
                ("big", "большой", "This is a big house."),
                ("small", "маленький", "A small cat."),
                ("happy", "счастливый", "I'm so happy!"),
                ("work", "работа", "I go to work every day."),
                ("learn", "учить", "I want to learn English.")
            };

            foreach (var (orig, trans, example) in starterWords)
            {
                starterDictionary.Words.Add(new Word
                {
                    UserId = userId,
                    OriginalWord = orig,
                    Translation = trans,
                    Example = example,
                    AddedAt = DateTime.UtcNow
                });
            }

            _context.Dictionaries.Add(starterDictionary);
            await _context.SaveChangesAsync();

            _logger.LogInformation("Starter pack created for User={UserId}, DictionaryId={DictionaryId}", 
                userId, starterDictionary.Id);

            return Ok(new 
            { 
                message = "Стартовый набор успешно создан!",
                dictionaryId = starterDictionary.Id,
                wordCount = starterWords.Count
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating starter pack for User={UserId}", userId);
            return StatusCode(500, "Произошла ошибка при создании стартового набора.");
        }
    }

    private async Task<int> CalculateStreakAsync(int userId, DateTime today)
    {
        var practiceDates = await _context.LearningProgresses
            .Where(p => p.UserId == userId && p.LastPracticed != default)
            .Select(p => p.LastPracticed.Date)
            .Distinct()
            .OrderByDescending(d => d)
            .Take(30) // Последние 30 дней достаточно для streak
            .ToListAsync();

        if (practiceDates.Count == 0)
            return 0;

        int streak = 0;
        var checkDate = today;

        // Если сегодня ещё не занимались, начинаем со вчера
        if (!practiceDates.Contains(today))
        {
            checkDate = today.AddDays(-1);
        }

        foreach (var _ in practiceDates)
        {
            if (practiceDates.Contains(checkDate))
            {
                streak++;
                checkDate = checkDate.AddDays(-1);
            }
            else
            {
                break;
            }
        }

        return streak;
    }

    private static TrainingWordDto MapToTrainingWord(LearningProgress p)
    {
        return new TrainingWordDto
        {
            WordId = p.WordId,
            OriginalWord = p.Word?.OriginalWord ?? "",
            Translation = p.Word?.Translation ?? "",
            Transcription = p.Word?.Transcription,
            Example = p.Word?.Example,
            DictionaryName = p.Word?.Dictionary?.Name ?? "",
            DictionaryId = p.Word?.DictionaryId ?? 0,
            KnowledgeLevel = p.KnowledgeLevel,
            NextReview = p.NextReview,
            TotalAttempts = p.TotalAttempts,
            CorrectAnswers = p.CorrectAnswers
        };
    }

    private static TrainingWordDto MapToTrainingWordProjection(LearningProgress p)
    {
        return new TrainingWordDto
        {
            WordId = p.WordId,
            OriginalWord = p.Word?.OriginalWord ?? "",
            Translation = p.Word?.Translation ?? "",
            Transcription = p.Word?.Transcription,
            Example = p.Word?.Example,
            DictionaryName = p.Word?.Dictionary?.Name ?? "",
            DictionaryId = p.Word?.DictionaryId ?? 0,
            KnowledgeLevel = p.KnowledgeLevel,
            NextReview = p.NextReview,
            TotalAttempts = p.TotalAttempts,
            CorrectAnswers = p.CorrectAnswers
        };
    }
}
