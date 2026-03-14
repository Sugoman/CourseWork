using LearningTrainerShared.Context;
using LearningTrainerShared.Models;
using LearningTrainerShared.Models.Statistics;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using System.Globalization;

namespace LearningAPI.Services;

public interface IStatisticsService
{
    Task<UserStatistics> GetFullStatisticsAsync(int userId, string period, CancellationToken ct = default);
    Task<List<DailyActivityStats>> GetDailyActivityAsync(int userId, int days, CancellationToken ct = default);
    Task<List<DictionaryStats>> GetDictionaryStatsAsync(int userId, CancellationToken ct = default);
    Task<List<DifficultWord>> GetDifficultWordsAsync(int userId, int limit, CancellationToken ct = default);
    Task<List<Achievement>> GetAchievementsAsync(int userId, CancellationToken ct = default);
    Task<List<UnlockedAchievementInfo>> SaveSessionAsync(int userId, SaveSessionRequest request, CancellationToken ct = default);
    Task UpdateUserStatsAsync(int userId, CancellationToken ct = default);
}

public class SaveSessionRequest
{
    public DateTime StartedAt { get; set; }
    public DateTime CompletedAt { get; set; }
    public int WordsReviewed { get; set; }
    public int CorrectAnswers { get; set; }
    public int WrongAnswers { get; set; }
    public string Mode { get; set; } = "Flashcards";
    public int? DictionaryId { get; set; }
}

/// <summary>
/// Информация о разблокированном достижении (§5.4 LEARNING_IMPROVEMENTS — Milestone-уведомления)
/// </summary>
public class UnlockedAchievementInfo
{
    public string Id { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string Icon { get; set; } = string.Empty;
    public string Rarity { get; set; } = string.Empty;
}

public class StatisticsService : IStatisticsService
{
    private readonly ApiDbContext _context;
    private readonly ILogger<StatisticsService> _logger;

    /// <summary>
    /// Get the user's TimeZoneInfo from their stored IANA timezone ID. Falls back to UTC.
    /// </summary>
    private async Task<TimeZoneInfo> GetUserTimeZoneAsync(int userId, CancellationToken ct)
    {
        var tzId = await _context.Users
            .Where(u => u.Id == userId)
            .Select(u => u.TimeZoneId)
            .FirstOrDefaultAsync(ct);

        if (string.IsNullOrEmpty(tzId))
            return TimeZoneInfo.Utc;

        try
        {
            return TimeZoneInfo.FindSystemTimeZoneById(tzId);
        }
        catch (TimeZoneNotFoundException)
        {
            return TimeZoneInfo.Utc;
        }
    }

    private static readonly string[] LevelNames = 
    {
        "Не начато",
        "Начато", 
        "Изучается",
        "Запоминается",
        "Почти выучено",
        "Выучено"
    };

    private static readonly string[] LevelColors =
    {
        "#9CA3AF", // Gray
        "#EF4444", // Red
        "#F59E0B", // Orange
        "#EAB308", // Yellow
        "#22C55E", // Green
        "#10B981"  // Emerald
    };

    public StatisticsService(ApiDbContext context, ILogger<StatisticsService> logger)
    {
        _context = context;
        _logger = logger;
    }

    public async Task<UserStatistics> GetFullStatisticsAsync(int userId, string period, CancellationToken ct = default)
    {
        var days = period switch
        {
            "week" => 7,
            "month" => 30,
            "3months" => 90,
            "year" => 365,
            _ => 9999 // all time
        };

        var fromDate = DateTime.UtcNow.Date.AddDays(-days);
        var userTz = await GetUserTimeZoneAsync(userId, ct);
        var today = TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, userTz).Date;

        // Основные данные прогресса
        var progresses = await _context.LearningProgresses
            .Where(p => p.UserId == userId)
            .Include(p => p.Word)
            .ThenInclude(w => w.Dictionary)
            .ToListAsync(ct);

        // Сессии за период
        var sessions = await _context.TrainingSessions
            .Where(s => s.UserId == userId && s.StartedAt >= fromDate)
            .ToListAsync(ct);

        // UserStats
        var userStats = await _context.UserStats.FindAsync(new object[] { userId }, ct);

        var stats = new UserStatistics
        {
            TotalWords = progresses.Count,
            LearnedWords = progresses.Count(p => p.KnowledgeLevel >= 4),
            InProgressWords = progresses.Count(p => p.KnowledgeLevel > 0 && p.KnowledgeLevel < 4),
            TotalDictionaries = progresses.Select(p => p.Word?.DictionaryId).Distinct().Count(),

            CurrentStreak = userStats?.CurrentStreak ?? await CalculateStreakAsync(userId, today, ct),
            BestStreak = userStats?.BestStreak ?? 0,
            LastPracticeDate = userStats?.LastPracticeDate,

            TotalSessions = sessions.Count,
            TotalLearningTime = TimeSpan.FromSeconds(userStats?.TotalLearningTimeSeconds ?? 
                sessions.Sum(s => (s.CompletedAt - s.StartedAt).TotalSeconds)),

            // §5.1 XP and levels
            TotalXp = userStats?.TotalXp ?? 0,
            Level = userStats?.Level ?? 1,
            XpForCurrentLevel = userStats?.XpForCurrentLevel ?? 0,
            XpForNextLevel = userStats?.XpForNextLevel ?? 50,
        };

        // Ответы за период
        stats.TotalCorrectAnswers = sessions.Sum(s => s.CorrectAnswers);
        stats.TotalWrongAnswers = sessions.Sum(s => s.WrongAnswers);

        // Точность за период (на основе сессий, а не общего LearningProgress)
        var totalSessionAnswers = stats.TotalCorrectAnswers + stats.TotalWrongAnswers;
        stats.OverallAccuracy = totalSessionAnswers > 0 
            ? (double)stats.TotalCorrectAnswers / totalSessionAnswers 
            : 0;

        // Среднее время на слово
        var totalWordsInSessions = sessions.Sum(s => s.WordsReviewed);
        stats.AverageSecondsPerWord = totalWordsInSessions > 0 
            ? stats.TotalLearningTime.TotalSeconds / totalWordsInSessions 
            : 0;

        // Средняя длительность сессии
        stats.AverageSessionDuration = sessions.Count > 0
            ? TimeSpan.FromSeconds(sessions.Average(s => (s.CompletedAt - s.StartedAt).TotalSeconds))
            : TimeSpan.Zero;

        // Прогресс за периоды
        stats.WordsLearnedToday = progresses.Count(p => 
            p.LastPracticed.Date == today && p.KnowledgeLevel >= 4);
        stats.WordsReviewedToday = progresses.Count(p => p.LastPracticed.Date == today);
        
        var weekStart = today.AddDays(-7);
        stats.WordsLearnedThisWeek = progresses.Count(p => 
            p.LastPracticed >= weekStart && p.KnowledgeLevel >= 4);
        
        var monthStart = today.AddDays(-30);
        stats.WordsLearnedThisMonth = progresses.Count(p => 
            p.LastPracticed >= monthStart && p.KnowledgeLevel >= 4);

        // Детализированные данные
        stats.DailyActivity = await GetDailyActivityAsync(userId, Math.Min(days, 30), ct);
        stats.HeatmapActivity = await GetDailyActivityAsync(userId, 365, ct);
        stats.WeeklyActivity = GetWeeklyActivity(sessions, days);
        stats.MonthlyProgress = GetMonthlyProgress(progresses, sessions, days);
        stats.DictionaryStatistics = await GetDictionaryStatsAsync(userId, ct);
        stats.KnowledgeDistribution = GetKnowledgeDistribution(progresses);
        stats.HourlyActivity = GetHourlyActivity(sessions);
        stats.DifficultWords = await GetDifficultWordsAsync(userId, 10, ct);
        stats.Achievements = await GetAchievementsAsync(userId, ct);
        stats.LeaderboardPosition = await GetLeaderboardPositionAsync(userId, ct);

        // §9.4 Retention rate per dictionary
        ComputeRetentionRates(stats.DictionaryStatistics, progresses);

        // Аналитика (§9 LEARNING_IMPROVEMENTS)
        stats.DictionaryForecasts = BuildDictionaryForecasts(stats.DictionaryStatistics, stats.WordsLearnedThisWeek);
        stats.OptimalTime = BuildOptimalTimeRecommendation(stats.HourlyActivity);
        stats.WeekOverWeek = BuildWeekComparison(sessions, progresses, today);

        return stats;
    }

    public async Task<List<DailyActivityStats>> GetDailyActivityAsync(int userId, int days, CancellationToken ct = default)
    {
        var fromDate = DateTime.UtcNow.Date.AddDays(-days + 1);
        
        var progresses = await _context.LearningProgresses
            .Where(p => p.UserId == userId && p.LastPracticed >= fromDate)
            .ToListAsync(ct);

        var sessions = await _context.TrainingSessions
            .Where(s => s.UserId == userId && s.StartedAt >= fromDate)
            .ToListAsync(ct);

        var result = new List<DailyActivityStats>();
        
        for (var i = 0; i < days; i++)
        {
            var date = DateTime.UtcNow.Date.AddDays(-days + 1 + i);
            var dayProgresses = progresses.Where(p => p.LastPracticed.Date == date).ToList();
            var daySessions = sessions.Where(s => s.StartedAt.Date == date).ToList();

            result.Add(new DailyActivityStats
            {
                Date = date,
                WordsReviewed = dayProgresses.Count,
                WordsLearned = dayProgresses.Count(p => p.KnowledgeLevel >= 4),
                NewWordsStarted = dayProgresses.Count(p => p.TotalAttempts == 1),
                CorrectAnswers = daySessions.Sum(s => s.CorrectAnswers),
                WrongAnswers = daySessions.Sum(s => s.WrongAnswers),
                TimeSpent = TimeSpan.FromSeconds(daySessions.Sum(s => (s.CompletedAt - s.StartedAt).TotalSeconds))
            });
        }

        return result;
    }

    public async Task<List<DictionaryStats>> GetDictionaryStatsAsync(int userId, CancellationToken ct = default)
    {
        // Один SQL-запрос: JOIN Dictionaries → Words → LearningProgresses, агрегация на сервере
        return await _context.Dictionaries
            .Where(d => d.Words.Any(w => w.Progress.Any(p => p.UserId == userId)))
            .Select(d => new DictionaryStats
            {
                DictionaryId = d.Id,
                DictionaryName = d.Name,
                TotalWords = d.Words.Count,
                LearnedWords = d.Words
                    .SelectMany(w => w.Progress)
                    .Count(p => p.UserId == userId && p.KnowledgeLevel >= 4),
                InProgressWords = d.Words
                    .SelectMany(w => w.Progress)
                    .Count(p => p.UserId == userId && p.KnowledgeLevel > 0 && p.KnowledgeLevel < 4),
                NotStartedWords = d.Words.Count - d.Words
                    .SelectMany(w => w.Progress)
                    .Count(p => p.UserId == userId),
                CompletionPercent = d.Words.Count > 0
                    ? d.Words.SelectMany(w => w.Progress)
                        .Count(p => p.UserId == userId && p.KnowledgeLevel >= 4) * 100.0 / d.Words.Count
                    : 0,
                Accuracy = d.Words.SelectMany(w => w.Progress)
                    .Where(p => p.UserId == userId)
                    .Sum(p => p.TotalAttempts) > 0
                        ? (double)d.Words.SelectMany(w => w.Progress)
                            .Where(p => p.UserId == userId)
                            .Sum(p => p.CorrectAnswers)
                          / d.Words.SelectMany(w => w.Progress)
                            .Where(p => p.UserId == userId)
                            .Sum(p => p.TotalAttempts)
                        : 0,
                LastPracticed = d.Words
                    .SelectMany(w => w.Progress)
                    .Where(p => p.UserId == userId)
                    .Max(p => (DateTime?)p.LastPracticed)
            })
            .OrderByDescending(d => d.LastPracticed)
            .ToListAsync(ct);
    }

    public async Task<List<DifficultWord>> GetDifficultWordsAsync(int userId, int limit, CancellationToken ct = default)
    {
        var difficultWords = await _context.LearningProgresses
            .Where(p => p.UserId == userId && p.TotalAttempts >= 3)
            .Include(p => p.Word)
            .ThenInclude(w => w.Dictionary)
            .OrderByDescending(p => (double)(p.TotalAttempts - p.CorrectAnswers) / p.TotalAttempts)
            .Take(limit)
            .Select(p => new DifficultWord
            {
                WordId = p.WordId,
                OriginalWord = p.Word.OriginalWord,
                Translation = p.Word.Translation,
                DictionaryName = p.Word.Dictionary != null ? p.Word.Dictionary.Name : "",
                TotalAttempts = p.TotalAttempts,
                WrongAttempts = p.TotalAttempts - p.CorrectAnswers,
                ErrorRate = p.TotalAttempts > 0 
                    ? (double)(p.TotalAttempts - p.CorrectAnswers) / p.TotalAttempts 
                    : 0,
                CurrentLevel = p.KnowledgeLevel
            })
            .ToListAsync(ct);

        return difficultWords.Where(w => w.ErrorRate > 0.3).ToList();
    }

    public async Task<List<Achievement>> GetAchievementsAsync(int userId, CancellationToken ct = default)
    {
        var userAchievements = await _context.UserAchievements
            .Where(a => a.UserId == userId)
            .ToListAsync(ct);

        var unlockedIds = userAchievements.ToDictionary(a => a.AchievementId, a => a);

        // Получаем текущие значения для расчёта прогресса
        var learnedWords = await _context.LearningProgresses
            .CountAsync(p => p.UserId == userId && p.KnowledgeLevel >= 4, ct);

        var userStats = await _context.UserStats.FindAsync(new object[] { userId }, ct);
        var currentStreak = userStats?.CurrentStreak ?? 0;

        var dictCount = await _context.Dictionaries
            .CountAsync(d => d.UserId == userId, ct);

        // Подсчёт скачиваний контента пользователя
        var userDictionaryIds = await _context.Dictionaries
            .Where(d => d.UserId == userId && d.IsPublished)
            .Select(d => d.Id)
            .ToListAsync(ct);

        var userRuleIds = await _context.Rules
            .Where(r => r.UserId == userId && r.IsPublished)
            .Select(r => r.Id)
            .ToListAsync(ct);

        var downloads = await _context.Downloads
            .CountAsync(d => 
                (d.ContentType == "Dictionary" && userDictionaryIds.Contains(d.ContentId)) ||
                (d.ContentType == "Rule" && userRuleIds.Contains(d.ContentId)), ct);

        // Авто-разблокировка достижений, которые выполнены, но ещё не записаны в БД
        var newlyUnlocked = new List<UserAchievement>();

        var result = AchievementDefinitions.All.Select(def =>
        {
            var isUnlocked = unlockedIds.ContainsKey(def.Id);
            var currentValue = GetAchievementCurrentValue(def, learnedWords, currentStreak, dictCount, downloads);
            var progress = def.TargetValue > 0 
                ? Math.Min(100, currentValue * 100.0 / def.TargetValue) 
                : 0;

            // Если условие выполнено, но в БД нет записи — разблокируем
            // (секретные ачивки не разблокируются автоматически здесь — только через CheckAndUnlockAchievementsAsync)
            if (!isUnlocked && !def.IsSecret && def.TargetValue > 0 && currentValue >= def.TargetValue)
            {
                isUnlocked = true;
                var ua = new UserAchievement
                {
                    UserId = userId,
                    AchievementId = def.Id,
                    UnlockedAt = DateTime.UtcNow
                };
                newlyUnlocked.Add(ua);
                unlockedIds[def.Id] = ua;
            }

            // Секретные ачивки: скрывать название/описание пока не разблокированы
            var title = def.IsSecret && !isUnlocked ? "???" : def.Title;
            var description = def.IsSecret && !isUnlocked ? (def.SecretHint ?? "Секретное достижение") : def.Description;
            var icon = def.IsSecret && !isUnlocked ? "🔒" : def.Icon;

            return new Achievement
            {
                Id = def.Id,
                Title = title,
                Description = description,
                Icon = icon,
                Category = def.Category,
                Rarity = def.Rarity,
                IsUnlocked = isUnlocked,
                UnlockedAt = isUnlocked ? unlockedIds[def.Id].UnlockedAt : null,
                Progress = isUnlocked ? 100 : (def.IsSecret && !isUnlocked ? 0 : progress),
                CurrentValue = isUnlocked ? def.TargetValue : (def.IsSecret ? null : currentValue),
                TargetValue = def.IsSecret && !isUnlocked ? null : def.TargetValue,
                ChainId = def.ChainId,
                ChainOrder = def.ChainOrder,
                IsSecret = def.IsSecret,
                SecretHint = def.IsSecret && !isUnlocked ? def.SecretHint : null
            };
        }).ToList();

        if (newlyUnlocked.Count > 0)
        {
            try
            {
                _context.UserAchievements.AddRange(newlyUnlocked);
                await _context.SaveChangesAsync(ct);
                _logger.LogInformation("Auto-unlocked {Count} achievements for User {UserId}", 
                    newlyUnlocked.Count, userId);
            }
            catch (DbUpdateException)
            {
                // Параллельный запрос уже вставил эти достижения — это нормально
                _logger.LogDebug("Achievement auto-unlock conflict for User {UserId}, already saved by another request", userId);
            }
        }

        return result;
    }

    public async Task<List<UnlockedAchievementInfo>> SaveSessionAsync(int userId, SaveSessionRequest request, CancellationToken ct = default)
    {
        var session = new TrainingSession
        {
            UserId = userId,
            StartedAt = request.StartedAt,
            CompletedAt = request.CompletedAt,
            WordsReviewed = request.WordsReviewed,
            CorrectAnswers = request.CorrectAnswers,
            WrongAnswers = request.WrongAnswers,
            Mode = request.Mode,
            DictionaryId = request.DictionaryId
        };

        _context.TrainingSessions.Add(session);
        await _context.SaveChangesAsync(ct);

        // Обновляем агрегированную статистику
        await UpdateUserStatsAsync(userId, ct);

        // Проверяем достижения и возвращаем новые (§5.4 Milestone-уведомления)
        var newAchievements = await CheckAndUnlockAchievementsAsync(userId, session, ct);
        return newAchievements;
    }

    public async Task UpdateUserStatsAsync(int userId, CancellationToken ct = default)
    {
        var userTz = await GetUserTimeZoneAsync(userId, ct);
        var today = TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, userTz).Date;
        var userStats = await _context.UserStats.FindAsync(new object[] { userId }, ct);

        // Агрегация на стороне БД вместо загрузки всех сессий в память
        var sessionAgg = await _context.TrainingSessions
            .Where(s => s.UserId == userId)
            .GroupBy(_ => 1)
            .Select(g => new
            {
                Count = g.Count(),
                TotalSeconds = g.Sum(s => EF.Functions.DateDiffSecond(s.StartedAt, s.CompletedAt))
            })
            .FirstOrDefaultAsync(ct);

        var currentStreak = await CalculateStreakAsync(userId, today, ct);

        if (userStats == null)
        {
            userStats = new UserStats
            {
                UserId = userId,
                CurrentStreak = currentStreak,
                BestStreak = currentStreak,
                LastPracticeDate = today,
                TotalLearningTimeSeconds = sessionAgg?.TotalSeconds ?? 0,
                TotalSessions = sessionAgg?.Count ?? 0,
                LastUpdated = DateTime.UtcNow
            };
            _context.UserStats.Add(userStats);
        }
        else
        {
            userStats.CurrentStreak = currentStreak;
            userStats.BestStreak = Math.Max(userStats.BestStreak, currentStreak);
            userStats.LastPracticeDate = today;
            userStats.TotalLearningTimeSeconds = sessionAgg?.TotalSeconds ?? 0;
            userStats.TotalSessions = sessionAgg?.Count ?? 0;
            userStats.LastUpdated = DateTime.UtcNow;
        }

        await _context.SaveChangesAsync(ct);
    }

    private async Task<int> CalculateStreakAsync(int userId, DateTime today, CancellationToken ct)
    {
        var practiceDates = await _context.LearningProgresses
            .Where(p => p.UserId == userId && p.LastPracticed != default)
            .Select(p => p.LastPracticed.Date)
            .Distinct()
            .OrderByDescending(d => d)
            .Take(400)
            .ToListAsync(ct);

        if (practiceDates.Count == 0) return 0;

        var practiceSet = practiceDates.ToHashSet();
        var streak = 0;
        var checkDate = today;

        if (!practiceSet.Contains(today))
        {
            checkDate = today.AddDays(-1);
        }

        while (practiceSet.Contains(checkDate))
        {
            streak++;
            checkDate = checkDate.AddDays(-1);
        }

        return streak;
    }

    private async Task<List<UnlockedAchievementInfo>> CheckAndUnlockAchievementsAsync(int userId, TrainingSession session, CancellationToken ct)
    {
        var existingAchievementsList = await _context.UserAchievements
            .Where(a => a.UserId == userId)
            .Select(a => a.AchievementId)
            .ToListAsync(ct);

        var existingAchievements = existingAchievementsList.ToHashSet();

        // Early exit — все ачивки уже разблокированы
        if (existingAchievements.Count >= AchievementDefinitions.All.Count)
            return new List<UnlockedAchievementInfo>();

        var newAchievements = new List<UserAchievement>();

        // --- Слова (проверяем только если есть незаблокированные word-ачивки) ---
        var wordIds = new[] { "first_word", "words_10", "words_50", "words_100", "words_500", "words_1000", "words_5000" };
        if (wordIds.Any(id => !existingAchievements.Contains(id)))
        {
            var learnedWords = await _context.LearningProgresses
                .CountAsync(p => p.UserId == userId && p.KnowledgeLevel >= 4, ct);
            CheckWordAchievements(learnedWords, existingAchievements, newAchievements, userId);
        }

        // --- Streak (читаем из уже обновлённого UserStats) ---
        var streakIds = new[] { "streak_3", "streak_7", "streak_30", "streak_100", "streak_365" };
        if (streakIds.Any(id => !existingAchievements.Contains(id)))
        {
            var userStats = await _context.UserStats.FindAsync(new object[] { userId }, ct);
            if (userStats != null)
            {
                CheckStreakAchievements(userStats.CurrentStreak, existingAchievements, newAchievements, userId);
            }
        }

        // --- Идеальная сессия ---
        if (session.WrongAnswers == 0 && session.WordsReviewed >= 10 && 
            !existingAchievements.Contains("perfect_session"))
        {
            newAchievements.Add(new UserAchievement
            {
                UserId = userId,
                AchievementId = "perfect_session",
                UnlockedAt = DateTime.UtcNow
            });
            existingAchievements.Add("perfect_session");
        }

        // --- Словари ---
        if (!existingAchievements.Contains("first_dict") || !existingAchievements.Contains("dict_5"))
        {
            var dictCount = await _context.Dictionaries.CountAsync(d => d.UserId == userId, ct);
            CheckMilestoneAchievement("first_dict", 1, dictCount, existingAchievements, newAchievements, userId);
            CheckMilestoneAchievement("dict_5", 5, dictCount, existingAchievements, newAchievements, userId);
        }

        // --- Точность (агрегация на стороне БД) ---
        if (!existingAchievements.Contains("accuracy_90") || !existingAchievements.Contains("accuracy_95"))
        {
            var accuracyAgg = await _context.LearningProgresses
                .Where(p => p.UserId == userId && p.TotalAttempts > 0)
                .GroupBy(_ => 1)
                .Select(g => new
                {
                    TotalAttempts = g.Sum(p => p.TotalAttempts),
                    TotalCorrect = g.Sum(p => p.CorrectAnswers)
                })
                .FirstOrDefaultAsync(ct);

            if (accuracyAgg is { TotalAttempts: > 0 })
            {
                var accuracyPercent = (int)(accuracyAgg.TotalCorrect * 100.0 / accuracyAgg.TotalAttempts);
                CheckMilestoneAchievement("accuracy_90", 90, accuracyPercent, existingAchievements, newAchievements, userId);
                CheckMilestoneAchievement("accuracy_95", 95, accuracyPercent, existingAchievements, newAchievements, userId);
            }
        }

        // --- Speed / Marathon (по текущей сессии, без доп. запросов) ---
        if (!existingAchievements.Contains("speed_demon"))
        {
            var sessionMinutes = (session.CompletedAt - session.StartedAt).TotalMinutes;
            if (session.WordsReviewed >= 50 && sessionMinutes <= 10)
            {
                newAchievements.Add(new UserAchievement
                {
                    UserId = userId,
                    AchievementId = "speed_demon",
                    UnlockedAt = DateTime.UtcNow
                });
                existingAchievements.Add("speed_demon");
            }
        }

        CheckMilestoneAchievement("marathon", 100, session.WordsReviewed, existingAchievements, newAchievements, userId);

        // --- Социальные ---
        if (!existingAchievements.Contains("share_first") || !existingAchievements.Contains("popular"))
        {
            var hasPublished = await _context.Dictionaries.AnyAsync(d => d.UserId == userId && d.IsPublished, ct)
                            || await _context.Rules.AnyAsync(r => r.UserId == userId && r.IsPublished, ct);
            if (hasPublished)
            {
                CheckMilestoneAchievement("share_first", 1, 1, existingAchievements, newAchievements, userId);
            }

            if (!existingAchievements.Contains("popular"))
            {
                var userDictIds = await _context.Dictionaries
                    .Where(d => d.UserId == userId && d.IsPublished)
                    .Select(d => d.Id).ToListAsync(ct);
                var userRuleIds = await _context.Rules
                    .Where(r => r.UserId == userId && r.IsPublished)
                    .Select(r => r.Id).ToListAsync(ct);

                if (userDictIds.Count > 0 || userRuleIds.Count > 0)
                {
                    var downloads = await _context.Downloads
                        .CountAsync(d =>
                            (d.ContentType == "Dictionary" && userDictIds.Contains(d.ContentId)) ||
                            (d.ContentType == "Rule" && userRuleIds.Contains(d.ContentId)), ct);
                    CheckMilestoneAchievement("popular", 100, downloads, existingAchievements, newAchievements, userId);
                }
            }
        }

        // === SECRET ACHIEVEMENTS ===

        // Convert session time to user's local timezone for time-of-day checks
        var userTz = await GetUserTimeZoneAsync(userId, ct);
        var localCompletedAt = TimeZoneInfo.ConvertTimeFromUtc(
            DateTime.SpecifyKind(session.CompletedAt, DateTimeKind.Utc), userTz);

        // 🦉 Ночная сова — тренировка после 23:00
        if (!existingAchievements.Contains("night_owl") && localCompletedAt.Hour >= 23)
        {
            newAchievements.Add(new UserAchievement
            {
                UserId = userId,
                AchievementId = "night_owl",
                UnlockedAt = DateTime.UtcNow
            });
            existingAchievements.Add("night_owl");
        }

        // 🐦 Ранняя пташка — тренировка до 06:00
        if (!existingAchievements.Contains("early_bird") && localCompletedAt.Hour < 6)
        {
            newAchievements.Add(new UserAchievement
            {
                UserId = userId,
                AchievementId = "early_bird",
                UnlockedAt = DateTime.UtcNow
            });
            existingAchievements.Add("early_bird");
        }

        // 💎 Перфекционист — 10 безупречных сессий подряд (мин. 10 слов)
        if (!existingAchievements.Contains("perfectionist"))
        {
            var recentSessions = await _context.TrainingSessions
                .Where(s => s.UserId == userId && s.WordsReviewed >= 10)
                .OrderByDescending(s => s.CompletedAt)
                .Take(10)
                .Select(s => new { s.WrongAnswers })
                .ToListAsync(ct);

            if (recentSessions.Count >= 10 && recentSessions.All(s => s.WrongAnswers == 0))
            {
                newAchievements.Add(new UserAchievement
                {
                    UserId = userId,
                    AchievementId = "perfectionist",
                    UnlockedAt = DateTime.UtcNow
                });
                existingAchievements.Add("perfectionist");
            }
        }

        // 🔄 Возвращение — вернуться после 30+ дней перерыва
        if (!existingAchievements.Contains("comeback_kid"))
        {
            var previousSession = await _context.TrainingSessions
                .Where(s => s.UserId == userId && s.Id != session.Id)
                .OrderByDescending(s => s.CompletedAt)
                .Select(s => s.CompletedAt)
                .FirstOrDefaultAsync(ct);

            if (previousSession != default && (session.StartedAt - previousSession).TotalDays >= 30)
            {
                newAchievements.Add(new UserAchievement
                {
                    UserId = userId,
                    AchievementId = "comeback_kid",
                    UnlockedAt = DateTime.UtcNow
                });
                existingAchievements.Add("comeback_kid");
            }
        }

        // 👑 Молниеносный — 100 слов за 10 минут
        if (!existingAchievements.Contains("speed_king"))
        {
            var sessionMinutes = (session.CompletedAt - session.StartedAt).TotalMinutes;
            if (session.WordsReviewed >= 100 && sessionMinutes <= 10)
            {
                newAchievements.Add(new UserAchievement
                {
                    UserId = userId,
                    AchievementId = "speed_king",
                    UnlockedAt = DateTime.UtcNow
                });
                existingAchievements.Add("speed_king");
            }
        }

        if (newAchievements.Count > 0)
        {
            _context.UserAchievements.AddRange(newAchievements);
            await _context.SaveChangesAsync(ct);

            _logger.LogInformation("User {UserId} unlocked {Count} new achievements", 
                userId, newAchievements.Count);
        }

        // §5.4 Milestone-уведомления: вернуть инфо о новых достижениях
        return newAchievements.Select(a =>
        {
            var def = AchievementDefinitions.GetById(a.AchievementId);
            return new UnlockedAchievementInfo
            {
                Id = a.AchievementId,
                Title = def?.Title ?? a.AchievementId,
                Description = def?.Description ?? "",
                Icon = def?.Icon ?? "🏆",
                Rarity = def?.Rarity.ToString() ?? "Common"
            };
        }).ToList();
    }

    private static void CheckMilestoneAchievement(string achievementId, int target, int currentValue,
        HashSet<string> existing, List<UserAchievement> newAchievements, int userId)
    {
        if (currentValue >= target && !existing.Contains(achievementId))
        {
            newAchievements.Add(new UserAchievement
            {
                UserId = userId,
                AchievementId = achievementId,
                UnlockedAt = DateTime.UtcNow
            });
            existing.Add(achievementId);
        }
    }

    private static void CheckWordAchievements(int learnedWords, HashSet<string> existing, 
        List<UserAchievement> newAchievements, int userId)
    {
        var wordMilestones = new (string id, int target)[]
        {
            ("first_word", 1),
            ("words_10", 10),
            ("words_50", 50),
            ("words_100", 100),
            ("words_500", 500),
            ("words_1000", 1000),
            ("words_5000", 5000)
        };

        foreach (var (id, target) in wordMilestones)
        {
            if (learnedWords >= target && !existing.Contains(id))
            {
                newAchievements.Add(new UserAchievement
                {
                    UserId = userId,
                    AchievementId = id,
                    UnlockedAt = DateTime.UtcNow
                });
            }
        }
    }

    private static void CheckStreakAchievements(int streak, HashSet<string> existing,
        List<UserAchievement> newAchievements, int userId)
    {
        var streakMilestones = new (string id, int target)[]
        {
            ("streak_3", 3),
            ("streak_7", 7),
            ("streak_30", 30),
            ("streak_100", 100),
            ("streak_365", 365)
        };

        foreach (var (id, target) in streakMilestones)
        {
            if (streak >= target && !existing.Contains(id))
            {
                newAchievements.Add(new UserAchievement
                {
                    UserId = userId,
                    AchievementId = id,
                    UnlockedAt = DateTime.UtcNow
                });
            }
        }
    }

    private static int GetAchievementCurrentValue(AchievementDefinition def, 
        int learnedWords, int currentStreak, int dictCount, int downloads)
    {
        return def.Id switch
        {
            "first_word" or "words_10" or "words_50" or "words_100" 
                or "words_500" or "words_1000" or "words_5000" => learnedWords,
            "streak_3" or "streak_7" or "streak_30" or "streak_100" or "streak_365" => currentStreak,
            "first_dict" or "dict_5" => dictCount,
            "popular" => downloads,
            _ => 0
        };
    }

    private static List<KnowledgeLevelDistribution> GetKnowledgeDistribution(List<LearningProgress> progresses)
    {
        // SM-2: KnowledgeLevel может быть > 5, группируем 5+ в «Выучено»
        var grouped = progresses
            .GroupBy(p => Math.Min(p.KnowledgeLevel, 5))
            .ToDictionary(g => g.Key, g => g.Count());

        var total = progresses.Count;

        return Enumerable.Range(0, 6)
            .Select(level => new KnowledgeLevelDistribution
            {
                Level = level,
                LevelName = LevelNames[level],
                Count = grouped.GetValueOrDefault(level, 0),
                Percentage = total > 0 ? grouped.GetValueOrDefault(level, 0) * 100.0 / total : 0,
                Color = LevelColors[level]
            })
            .ToList();
    }

    private static List<HourlyActivityStats> GetHourlyActivity(List<TrainingSession> sessions)
    {
        var hourlyData = sessions
            .GroupBy(s => s.StartedAt.Hour)
            .ToDictionary(
                g => g.Key,
                g => new { Words = g.Sum(s => s.WordsReviewed), Correct = g.Sum(s => s.CorrectAnswers), Total = g.Sum(s => s.WordsReviewed) }
            );

        return Enumerable.Range(0, 24)
            .Select(hour =>
            {
                var data = hourlyData.GetValueOrDefault(hour);
                return new HourlyActivityStats
                {
                    Hour = hour,
                    WordsReviewed = data?.Words ?? 0,
                    AverageAccuracy = data?.Total > 0 ? (double)data.Correct / data.Total : 0
                };
            })
            .ToList();
    }

    private static List<WeeklyActivityStats> GetWeeklyActivity(List<TrainingSession> sessions, int days)
    {
        var weeks = (int)Math.Ceiling(days / 7.0);
        var result = new List<WeeklyActivityStats>();

        for (var w = 0; w < weeks; w++)
        {
            var weekEnd = DateTime.UtcNow.Date.AddDays(-w * 7);
            var weekStart = weekEnd.AddDays(-6);

            var weekSessions = sessions
                .Where(s => s.StartedAt.Date >= weekStart && s.StartedAt.Date <= weekEnd)
                .ToList();

            var totalAttempts = weekSessions.Sum(s => s.WordsReviewed);
            var correctAnswers = weekSessions.Sum(s => s.CorrectAnswers);

            result.Add(new WeeklyActivityStats
            {
                WeekNumber = CultureInfo.CurrentCulture.Calendar.GetWeekOfYear(
                    weekStart, CalendarWeekRule.FirstDay, DayOfWeek.Monday),
                Year = weekStart.Year,
                WeekStart = weekStart,
                WeekEnd = weekEnd,
                TotalWordsReviewed = weekSessions.Sum(s => s.WordsReviewed),
                TotalWordsLearned = 0, // Требует дополнительных данных
                DaysActive = weekSessions.Select(s => s.StartedAt.Date).Distinct().Count(),
                AverageAccuracy = totalAttempts > 0 ? (double)correctAnswers / totalAttempts : 0,
                TotalTimeSpent = TimeSpan.FromSeconds(weekSessions.Sum(s => (s.CompletedAt - s.StartedAt).TotalSeconds))
            });
        }

        return result.OrderBy(w => w.WeekStart).ToList();
    }

    private static List<MonthlyProgressStats> GetMonthlyProgress(
        List<LearningProgress> progresses, 
        List<TrainingSession> sessions, 
        int days)
    {
        var months = (int)Math.Ceiling(days / 30.0);
        var result = new List<MonthlyProgressStats>();
        var culture = new CultureInfo("ru-RU");

        for (var m = 0; m < months; m++)
        {
            var monthDate = DateTime.UtcNow.AddMonths(-m);
            var monthStart = new DateTime(monthDate.Year, monthDate.Month, 1);
            var monthEnd = monthStart.AddMonths(1).AddDays(-1);

            var monthProgresses = progresses
                .Where(p => p.LastPracticed >= monthStart && p.LastPracticed <= monthEnd)
                .ToList();

            var monthSessions = sessions
                .Where(s => s.StartedAt >= monthStart && s.StartedAt <= monthEnd)
                .ToList();

            var totalAttempts = monthProgresses.Sum(p => p.TotalAttempts);
            var correctAnswers = monthProgresses.Sum(p => p.CorrectAnswers);

            result.Add(new MonthlyProgressStats
            {
                Month = monthDate.Month,
                Year = monthDate.Year,
                MonthName = culture.DateTimeFormat.GetMonthName(monthDate.Month),
                WordsLearned = monthProgresses.Count(p => p.KnowledgeLevel >= 4),
                WordsReviewed = monthProgresses.Count,
                DaysActive = monthProgresses.Select(p => p.LastPracticed.Date).Distinct().Count(),
                AverageAccuracy = totalAttempts > 0 ? (double)correctAnswers / totalAttempts * 100 : 0
            });
        }

        return result.OrderBy(m => new DateTime(m.Year, m.Month, 1)).ToList();
    }

    private async Task<LeaderboardPosition?> GetLeaderboardPositionAsync(int userId, CancellationToken ct)
    {
        // Global ranking: by total learned words (KnowledgeLevel >= 4), exclude users with 0
        var allUserStats = await _context.LearningProgresses
            .GroupBy(p => p.UserId)
            .Select(g => new { UserId = g.Key, LearnedCount = g.Count(p => p.KnowledgeLevel >= 4) })
            .Where(x => x.LearnedCount > 0)
            .OrderByDescending(x => x.LearnedCount)
            .ToListAsync(ct);

        var totalUsers = allUserStats.Count;
        var userPosition = allUserStats.FindIndex(x => x.UserId == userId);

        if (userPosition == -1) return null;

        // Weekly ranking: by correct answers this week
        var weekStart = DateTime.UtcNow.Date.AddDays(-(int)DateTime.UtcNow.DayOfWeek + 1);
        if (DateTime.UtcNow.DayOfWeek == DayOfWeek.Sunday)
            weekStart = weekStart.AddDays(-7);

        var weeklyStats = await _context.TrainingSessions
            .Where(s => s.CompletedAt >= weekStart)
            .GroupBy(s => s.UserId)
            .Select(g => new { UserId = g.Key, WeeklyPoints = g.Sum(s => s.CorrectAnswers) })
            .Where(x => x.WeeklyPoints > 0)
            .OrderByDescending(x => x.WeeklyPoints)
            .ToListAsync(ct);

        var weeklyPosition = weeklyStats.FindIndex(x => x.UserId == userId);

        return new LeaderboardPosition
        {
            GlobalRank = userPosition + 1,
            TotalUsers = totalUsers,
            // Percentile: #1 из 100 = Топ 1%, #50 из 100 = Топ 50%
            Percentile = totalUsers > 1 ? (double)(userPosition + 1) / totalUsers * 100 : 100,
            WeeklyRank = weeklyPosition >= 0 ? weeklyPosition + 1 : 0,
            WeeklyPoints = weeklyPosition >= 0 ? weeklyStats[weeklyPosition].WeeklyPoints : 0
        };
    }

    // === Аналитика (§9 LEARNING_IMPROVEMENTS) ===

    /// <summary>
    /// §9.1 Прогноз завершения словаря по текущему темпу.
    /// </summary>
    private static List<DictionaryForecast> BuildDictionaryForecasts(
        List<DictionaryStats> dictionaryStats, int wordsLearnedThisWeek)
    {
        var wordsPerDay = wordsLearnedThisWeek / 7.0;
        if (wordsPerDay <= 0) wordsPerDay = 0;

        return dictionaryStats
            .Where(d => d.TotalWords > 0 && d.CompletionPercent < 100)
            .Select(d =>
            {
                var remaining = d.TotalWords - d.LearnedWords;
                int? estimatedDays = wordsPerDay > 0 ? (int)Math.Ceiling(remaining / wordsPerDay) : null;
                return new DictionaryForecast
                {
                    DictionaryId = d.DictionaryId,
                    DictionaryName = d.DictionaryName,
                    TotalWords = d.TotalWords,
                    LearnedWords = d.LearnedWords,
                    RemainingWords = remaining,
                    WordsPerDay = Math.Round(wordsPerDay, 1),
                    EstimatedDaysToComplete = estimatedDays,
                    EstimatedCompletionDate = estimatedDays.HasValue
                        ? DateTime.UtcNow.Date.AddDays(estimatedDays.Value)
                        : null
                };
            })
            .OrderBy(f => f.EstimatedDaysToComplete ?? int.MaxValue)
            .ToList();
    }

    /// <summary>
    /// §9.2 Рекомендация оптимального времени для занятий.
    /// </summary>
    private static OptimalTimeRecommendation? BuildOptimalTimeRecommendation(
        List<HourlyActivityStats> hourlyActivity)
    {
        var activeHours = hourlyActivity.Where(h => h.WordsReviewed >= 3).ToList();
        if (activeHours.Count < 2) return null;

        var best = activeHours.OrderByDescending(h => h.AverageAccuracy).First();
        var worst = activeHours.OrderBy(h => h.AverageAccuracy).First();

        return new OptimalTimeRecommendation
        {
            BestHour = best.Hour,
            BestAccuracy = Math.Round(best.AverageAccuracy * 100, 1),
            WorstHour = worst.Hour,
            WorstAccuracy = Math.Round(worst.AverageAccuracy * 100, 1),
            Recommendation = $"Ваша точность максимальна в {best.Hour}:00–{best.Hour + 1}:00 ({Math.Round(best.AverageAccuracy * 100)}%). " +
                             $"Рекомендуем заниматься в это время."
        };
    }

    /// <summary>
    /// §9.3 Сравнение текущей и предыдущей недели.
    /// </summary>
    private static WeekComparison BuildWeekComparison(
        List<TrainingSession> sessions, List<LearningProgress> progresses, DateTime today)
    {
        var currentWeekStart = today.AddDays(-6);
        var previousWeekStart = today.AddDays(-13);

        var currentSessions = sessions.Where(s => s.StartedAt.Date >= currentWeekStart).ToList();
        var previousSessions = sessions.Where(s => s.StartedAt.Date >= previousWeekStart && s.StartedAt.Date < currentWeekStart).ToList();

        var currentWords = progresses.Count(p => p.LastPracticed.Date >= currentWeekStart);
        var previousWords = progresses.Count(p => p.LastPracticed.Date >= previousWeekStart && p.LastPracticed.Date < currentWeekStart);

        var diff = currentWords - previousWords;
        var pctChange = previousWords > 0 ? (double)diff / previousWords * 100 : (currentWords > 0 ? 100 : 0);

        var currentCorrect = currentSessions.Sum(s => s.CorrectAnswers);
        var currentTotal = currentCorrect + currentSessions.Sum(s => s.WrongAnswers);
        var previousCorrect = previousSessions.Sum(s => s.CorrectAnswers);
        var previousTotal = previousCorrect + previousSessions.Sum(s => s.WrongAnswers);

        return new WeekComparison
        {
            CurrentWeekWords = currentWords,
            PreviousWeekWords = previousWords,
            WordsDifference = diff,
            PercentChange = Math.Round(pctChange, 1),
            CurrentWeekDaysActive = currentSessions.Select(s => s.StartedAt.Date).Distinct().Count(),
            PreviousWeekDaysActive = previousSessions.Select(s => s.StartedAt.Date).Distinct().Count(),
            CurrentWeekAccuracy = currentTotal > 0 ? Math.Round((double)currentCorrect / currentTotal * 100, 1) : 0,
            PreviousWeekAccuracy = previousTotal > 0 ? Math.Round((double)previousCorrect / previousTotal * 100, 1) : 0,
            IsImproving = diff > 0
        };
    }

    /// <summary>
    /// §9.4 Retention rate per dictionary.
    /// Доля слов с KnowledgeLevel >= 3 среди тех, что начали учить > 30 дней назад.
    /// </summary>
    private static void ComputeRetentionRates(
        List<DictionaryStats> dictionaryStats, List<LearningProgress> progresses)
    {
        var threshold = DateTime.UtcNow.AddDays(-30);

        // Group progresses by DictionaryId (via Word)
        var progressByDict = progresses
            .Where(p => p.Word?.DictionaryId != null && p.TotalAttempts > 0)
            .GroupBy(p => p.Word!.DictionaryId);

        foreach (var group in progressByDict)
        {
            var dict = dictionaryStats.FirstOrDefault(d => d.DictionaryId == group.Key);
            if (dict == null) continue;

            // Only consider words started > 30 days ago
            var matureWords = group.Where(p => p.LastPracticed < threshold && p.TotalAttempts >= 2).ToList();
            if (matureWords.Count < 3) continue; // Need at least 3 words for meaningful rate

            var retained = matureWords.Count(p => p.KnowledgeLevel >= 3);
            dict.RetentionRate = Math.Round((double)retained / matureWords.Count * 100, 1);
        }
    }
}
