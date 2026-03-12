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
        [FromQuery] int reviewLimit = DefaultReviewLimit,
        CancellationToken ct = default)
    {
        var userId = GetUserId();
        var now = DateTime.UtcNow;
        var today = now.Date;

        _logger.LogInformation("Getting daily plan for User={UserId}", userId);

        try
        {
            // --- 1. Слова к повторению — исключаем замороженные, сортируем по overdue factor (§2.3) ---
            // Overdue factor = (now - NextReview) / max(IntervalDays, 1) — чем выше, тем срочнее
            var reviewCandidates = await _context.LearningProgresses
                .Include(p => p.Word).ThenInclude(w => w.Dictionary)
                .Where(p => p.UserId == userId && p.NextReview <= now && p.Word != null && !p.IsSuspended)
                .ToListAsync();

            var reviewWords = reviewCandidates
                .OrderByDescending(p => (now - p.NextReview).TotalDays / Math.Max(p.IntervalDays, 1.0))
                .Take(reviewLimit)
                .Select(p => MapToTrainingWordProjection(p))
                .ToList();

            // --- 2. Сложные слова — исключаем замороженные ---
            var difficultWords = await _context.LearningProgresses
                .Include(p => p.Word).ThenInclude(w => w.Dictionary)
                .Where(p => p.UserId == userId && p.Word != null && !p.IsSuspended &&
                           (p.KnowledgeLevel == 0 ||
                            (p.TotalAttempts > 2 && p.CorrectAnswers < p.TotalAttempts / 2)))
                .OrderBy(p => p.KnowledgeLevel)
                .Take(10)
                .Select(p => MapToTrainingWordProjection(p))
                .ToListAsync();

            // --- 3. Агрегированная статистика — один SQL-запрос вместо четырёх ---
            var stats = await _context.LearningProgresses
                .Where(p => p.UserId == userId)
                .GroupBy(_ => 1)
                .Select(g => new
                {
                    TotalProgress = g.Count(),
                    TotalReviewCount = g.Count(p => p.NextReview <= now && !p.IsSuspended),
                    CompletedToday = g.Count(p => p.LastPracticed >= today),
                    LeechCount = g.Count(p => p.IsSuspended),
                    LastPractice = g.Where(p => p.LastPracticed != default)
                                    .Max(p => (DateTime?)p.LastPracticed)
                })
                .FirstOrDefaultAsync();

            var totalWordCount = await _context.Words
                .CountAsync(w => w.UserId == userId);

            // --- 4. Новые слова — с адаптивным лимитом (§2.1 LEARNING_IMPROVEMENTS) ---
            // Если точность за сегодня низкая — сокращаем поток новых слов
            var completedToday = stats?.CompletedToday ?? 0;
            var adaptiveNewLimit = newWordsLimit;
            if (completedToday >= 5) // достаточно данных для оценки
            {
                // Считаем accuracy: слова без сброса / все сегодняшние
                var todayResetCount = await _context.LearningProgresses
                    .CountAsync(p => p.UserId == userId && p.LastPracticed >= today && p.KnowledgeLevel == 0 && p.TotalAttempts > 0);
                var todayAccuracy = 1.0 - (double)todayResetCount / completedToday;
                if (todayAccuracy < 0.6)
                    adaptiveNewLimit = Math.Max(2, newWordsLimit / 3);
                else if (todayAccuracy < 0.8)
                    adaptiveNewLimit = Math.Max(3, newWordsLimit / 2);
            }

            var newWords = await _context.Words
                .Where(w => w.UserId == userId &&
                       !_context.LearningProgresses
                            .Where(p => p.UserId == userId)
                            .Select(p => p.WordId)
                            .Contains(w.Id))
                .OrderBy(w => w.AddedAt)
                .Take(adaptiveNewLimit)
                .Select(w => new TrainingWordDto
                {
                    WordId = w.Id,
                    OriginalWord = w.OriginalWord,
                    Translation = w.Translation,
                    Transcription = w.Transcription,
                    Example = w.Example,
                    DictionaryName = w.Dictionary != null ? w.Dictionary.Name : "",
                    DictionaryId = w.DictionaryId,
                    DictionaryTags = w.Dictionary != null ? w.Dictionary.Tags : null,
                    LanguageFrom = w.Dictionary != null ? w.Dictionary.LanguageFrom : null,
                    KnowledgeLevel = 0,
                    NextReview = null,
                    TotalAttempts = 0,
                    CorrectAnswers = 0
                })
                .ToListAsync();

            // --- 5. Streak ---
            var streak = await CalculateStreakAsync(userId, today);

            // --- 6. Daily Goal из UserStats ---
            var userStats = await _context.UserStats.FindAsync(userId);
            var dailyGoal = userStats?.DailyGoal ?? 20;

            var plan = new DailyPlanDto
            {
                ReviewWords = reviewWords,
                NewWords = newWords,
                DifficultWords = difficultWords,
                Stats = new DailyPlanStats
                {
                    TotalReviewCount = stats?.TotalReviewCount ?? 0,
                    TotalNewCount = totalWordCount - (stats?.TotalProgress ?? 0),
                    TotalDifficultCount = difficultWords.Count,
                    CompletedToday = stats?.CompletedToday ?? 0,
                    CurrentStreak = streak,
                    LastPracticeDate = stats?.LastPractice,
                    LeechCount = stats?.LeechCount ?? 0,
                    DailyGoal = dailyGoal
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
        [FromQuery] string? tag = null,
        [FromQuery] int limit = 20,
        CancellationToken ct = default)
    {
        var userId = GetUserId();
        var now = DateTime.UtcNow;

        try
        {
            IQueryable<LearningProgress> query = _context.LearningProgresses
                .Where(p => p.UserId == userId && !p.IsSuspended)
                .Include(p => p.Word)
                    .ThenInclude(w => w.Dictionary)
                .Include(p => p.Word)
                    .ThenInclude(w => w.RelatedRule);

            if (dictionaryId.HasValue)
            {
                query = query.Where(p => p.Word.DictionaryId == dictionaryId.Value);
            }

            // §4.2 Серверная фильтрация по тегам — кросс-словарные тематические сессии
            if (!string.IsNullOrWhiteSpace(tag))
            {
                query = query.Where(p => p.Word.Dictionary != null && p.Word.Dictionary.Tags != null && p.Word.Dictionary.Tags.Contains(tag));
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

                case "weakness":
                    // §19.4 — Слова с низкой точностью, но ещё не leeches
                    result = await query
                        .Where(p => p.TotalAttempts >= 3
                                    && p.KnowledgeLevel <= 2
                                    && p.CorrectAnswers < (int)(p.TotalAttempts * 0.6))
                        .OrderBy(p => (double)p.CorrectAnswers / p.TotalAttempts)
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

                    if (!string.IsNullOrWhiteSpace(tag))
                    {
                        newWordsQuery = newWordsQuery.Where(w => w.Dictionary != null && w.Dictionary.Tags != null && w.Dictionary.Tags.Contains(tag));
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
                            DictionaryTags = w.Dictionary != null ? w.Dictionary.Tags : null,
                            LanguageFrom = w.Dictionary != null ? w.Dictionary.LanguageFrom : null,
                            KnowledgeLevel = 0,
                            TotalAttempts = 0,
                            CorrectAnswers = 0,
                            RelatedRuleId = w.RelatedRuleId,
                            RelatedRuleTitle = w.RelatedRule != null ? w.RelatedRule.Title : null
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

                    if (!string.IsNullOrWhiteSpace(tag))
                    {
                        newWordsForMixed = newWordsForMixed.Where(w => w.Dictionary != null && w.Dictionary.Tags != null && w.Dictionary.Tags.Contains(tag));
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
                            DictionaryTags = w.Dictionary != null ? w.Dictionary.Tags : null,
                            LanguageFrom = w.Dictionary != null ? w.Dictionary.LanguageFrom : null,
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
    /// Получить ежедневный челлендж пользователя (§5.2 LEARNING_IMPROVEMENTS)
    /// </summary>
    [HttpGet("daily-challenge")]
    public async Task<ActionResult<DailyChallengeDto>> GetDailyChallenge(CancellationToken ct = default)
    {
        var userId = GetUserId();
        var today = DateTime.UtcNow.Date;

        try
        {
            // Детерминированный выбор челленджа из seed = дата
            var seed = today.Year * 10000 + today.Month * 100 + today.Day;
            var rng = new Random(seed);

            var challenges = new (string id, string title, string desc, string icon, int target, int xp)[]
            {
                ("words_5_new", "Первопроходец", "Изучите 5 новых слов", "🆕", 5, 30),
                ("streak_correct_10", "Безупречность", "Ответьте на 10 слов подряд без ошибок", "🎯", 10, 50),
                ("review_20", "Повторение — мать учения", "Повторите 20 слов", "🔄", 20, 25),
                ("time_5min", "Пятиминутка", "Занимайтесь хотя бы 5 минут", "⏱️", 5, 20),
                ("words_today_15", "Марафонец", "Пройдите 15 слов за сегодня", "🏃", 15, 35),
                ("accuracy_80", "Снайпер", "Достигните точности 80%+ за сессию", "🎯", 80, 40),
            };

            var challenge = challenges[rng.Next(challenges.Length)];

            // Подсчёт текущего прогресса
            int currentValue = 0;
            switch (challenge.id)
            {
                case "words_5_new":
                    // Новые слова сегодня = прогрессы созданные сегодня с KnowledgeLevel == 0 или 1
                    currentValue = await _context.LearningProgresses
                        .CountAsync(p => p.UserId == userId && p.LastPracticed >= today && p.TotalAttempts <= 1, ct);
                    break;
                case "review_20":
                case "words_today_15":
                    currentValue = await _context.LearningProgresses
                        .CountAsync(p => p.UserId == userId && p.LastPracticed >= today, ct);
                    break;
                case "streak_correct_10":
                    // Approximation: correct answers today from sessions
                    var todaySessions = await _context.TrainingSessions
                        .Where(s => s.UserId == userId && s.StartedAt >= today)
                        .ToListAsync(ct);
                    currentValue = todaySessions.Sum(s => s.CorrectAnswers);
                    break;
                case "time_5min":
                    var todayTimeSessions = await _context.TrainingSessions
                        .Where(s => s.UserId == userId && s.StartedAt >= today)
                        .ToListAsync(ct);
                    var todayTimeSeconds = todayTimeSessions.Sum(s => (int)(s.CompletedAt - s.StartedAt).TotalSeconds);
                    currentValue = todayTimeSeconds / 60; // minutes
                    break;
                case "accuracy_80":
                    var sessionsToday = await _context.TrainingSessions
                        .Where(s => s.UserId == userId && s.StartedAt >= today)
                        .ToListAsync(ct);
                    var totalAns = sessionsToday.Sum(s => s.CorrectAnswers + s.WrongAnswers);
                    currentValue = totalAns > 0 ? (int)Math.Round((double)sessionsToday.Sum(s => s.CorrectAnswers) / totalAns * 100) : 0;
                    break;
            }

            return Ok(new DailyChallengeDto
            {
                Id = challenge.id,
                Title = challenge.title,
                Description = challenge.desc,
                Icon = challenge.icon,
                TargetValue = challenge.target,
                CurrentValue = Math.Min(currentValue, challenge.target),
                XpReward = challenge.xp
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting daily challenge for User={UserId}", userId);
            return StatusCode(500, "Ошибка при получении ежедневного челленджа.");
        }
    }

    /// <summary>
    /// Получить стартовый набор контента для новичков
    /// </summary>
    [HttpPost("starter-pack")]
    public async Task<ActionResult> GetStarterPack(CancellationToken ct = default)
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
        // Получаем уникальные даты практики (серверная проекция по дню)
        var practiceDates = await _context.LearningProgresses
            .Where(p => p.UserId == userId && p.LastPracticed != default
                        && p.LastPracticed >= today.AddDays(-30))
            .Select(p => p.LastPracticed)
            .ToListAsync();

        if (practiceDates.Count == 0)
            return 0;

        // Проекция .Date на клиенте (после загрузки минимального набора)
        var uniqueDates = practiceDates
            .Select(d => d.Date)
            .Distinct()
            .ToHashSet();

        int streak = 0;
        var checkDate = today;

        // Если сегодня ещё не занимались, начинаем со вчера
        if (!uniqueDates.Contains(today))
        {
            checkDate = today.AddDays(-1);
        }

        while (uniqueDates.Contains(checkDate))
        {
            streak++;
            checkDate = checkDate.AddDays(-1);
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
            DictionaryTags = p.Word?.Dictionary?.Tags,
            LanguageFrom = p.Word?.Dictionary?.LanguageFrom,
            LanguageTo = p.Word?.Dictionary?.LanguageTo,
            KnowledgeLevel = p.KnowledgeLevel,
            NextReview = p.NextReview,
            TotalAttempts = p.TotalAttempts,
            CorrectAnswers = p.CorrectAnswers,
            LapseCount = p.LapseCount,
            IsLeech = p.IsSuspended,
            UserNote = p.UserNote,
            RelatedRuleId = p.Word?.RelatedRuleId,
            RelatedRuleTitle = p.Word?.RelatedRule?.Title
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
            DictionaryTags = p.Word?.Dictionary?.Tags,
            LanguageFrom = p.Word?.Dictionary?.LanguageFrom,
            LanguageTo = p.Word?.Dictionary?.LanguageTo,
            KnowledgeLevel = p.KnowledgeLevel,
            NextReview = p.NextReview,
            TotalAttempts = p.TotalAttempts,
            CorrectAnswers = p.CorrectAnswers,
            LapseCount = p.LapseCount,
            IsLeech = p.IsSuspended,
            UserNote = p.UserNote,
            RelatedRuleId = p.Word?.RelatedRuleId,
            RelatedRuleTitle = p.Word?.RelatedRule?.Title
        };
    }
}
