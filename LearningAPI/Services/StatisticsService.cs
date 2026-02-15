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
    Task SaveSessionAsync(int userId, SaveSessionRequest request, CancellationToken ct = default);
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

public class StatisticsService : IStatisticsService
{
    private readonly ApiDbContext _context;
    private readonly ILogger<StatisticsService> _logger;

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
        var today = DateTime.UtcNow.Date;

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
        };

        // Точность
        var totalAttempts = progresses.Sum(p => p.TotalAttempts);
        var totalCorrect = progresses.Sum(p => p.CorrectAnswers);
        stats.OverallAccuracy = totalAttempts > 0 ? (double)totalCorrect / totalAttempts : 0;

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
        stats.WeeklyActivity = GetWeeklyActivity(sessions, days);
        stats.MonthlyProgress = GetMonthlyProgress(progresses, sessions, days);
        stats.DictionaryStatistics = await GetDictionaryStatsAsync(userId, ct);
        stats.KnowledgeDistribution = GetKnowledgeDistribution(progresses);
        stats.HourlyActivity = GetHourlyActivity(sessions);
        stats.DifficultWords = await GetDifficultWordsAsync(userId, 10, ct);
        stats.Achievements = await GetAchievementsAsync(userId, ct);
        stats.LeaderboardPosition = await GetLeaderboardPositionAsync(userId, ct);

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
        var userWordIds = await _context.LearningProgresses
            .Where(p => p.UserId == userId)
            .Select(p => p.WordId)
            .ToListAsync(ct);

        var dictionaries = await _context.Dictionaries
            .Where(d => d.Words.Any(w => userWordIds.Contains(w.Id)))
            .Include(d => d.Words)
            .ToListAsync(ct);

        var progresses = await _context.LearningProgresses
            .Where(p => p.UserId == userId)
            .ToListAsync(ct);

        var progressLookup = progresses.ToDictionary(p => p.WordId);

        return dictionaries.Select(d =>
        {
            var dictWordIds = d.Words.Select(w => w.Id).ToHashSet();
            var dictProgresses = progresses.Where(p => dictWordIds.Contains(p.WordId)).ToList();
            
            var totalAttempts = dictProgresses.Sum(p => p.TotalAttempts);
            var correctAnswers = dictProgresses.Sum(p => p.CorrectAnswers);

            return new DictionaryStats
            {
                DictionaryId = d.Id,
                DictionaryName = d.Name,
                TotalWords = d.Words.Count,
                LearnedWords = dictProgresses.Count(p => p.KnowledgeLevel >= 4),
                InProgressWords = dictProgresses.Count(p => p.KnowledgeLevel > 0 && p.KnowledgeLevel < 4),
                NotStartedWords = d.Words.Count - dictProgresses.Count,
                CompletionPercent = d.Words.Count > 0 
                    ? dictProgresses.Count(p => p.KnowledgeLevel >= 4) * 100.0 / d.Words.Count 
                    : 0,
                Accuracy = totalAttempts > 0 ? (double)correctAnswers / totalAttempts : 0,
                LastPracticed = dictProgresses.Any() 
                    ? dictProgresses.Max(p => p.LastPracticed) 
                    : null
            };
        }).OrderByDescending(d => d.LastPracticed).ToList();
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

        return AchievementDefinitions.All.Select(def =>
        {
            var isUnlocked = unlockedIds.ContainsKey(def.Id);
            var currentValue = GetAchievementCurrentValue(def, learnedWords, currentStreak, dictCount, downloads);
            var progress = def.TargetValue > 0 
                ? Math.Min(100, currentValue * 100.0 / def.TargetValue) 
                : 0;

            return new Achievement
            {
                Id = def.Id,
                Title = def.Title,
                Description = def.Description,
                Icon = def.Icon,
                Category = def.Category,
                Rarity = def.Rarity,
                IsUnlocked = isUnlocked,
                UnlockedAt = isUnlocked ? unlockedIds[def.Id].UnlockedAt : null,
                Progress = isUnlocked ? 100 : progress,
                CurrentValue = isUnlocked ? def.TargetValue : currentValue,
                TargetValue = def.TargetValue
            };
        }).ToList();
    }

    public async Task SaveSessionAsync(int userId, SaveSessionRequest request, CancellationToken ct = default)
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

        // Проверяем достижения
        await CheckAndUnlockAchievementsAsync(userId, session, ct);
    }

    public async Task UpdateUserStatsAsync(int userId, CancellationToken ct = default)
    {
        var today = DateTime.UtcNow.Date;
        var userStats = await _context.UserStats.FindAsync(new object[] { userId }, ct);

        var sessions = await _context.TrainingSessions
            .Where(s => s.UserId == userId)
            .ToListAsync(ct);

        var currentStreak = await CalculateStreakAsync(userId, today, ct);

        if (userStats == null)
        {
            userStats = new UserStats
            {
                UserId = userId,
                CurrentStreak = currentStreak,
                BestStreak = currentStreak,
                LastPracticeDate = today,
                TotalLearningTimeSeconds = (long)sessions.Sum(s => (s.CompletedAt - s.StartedAt).TotalSeconds),
                TotalSessions = sessions.Count,
                LastUpdated = DateTime.UtcNow
            };
            _context.UserStats.Add(userStats);
        }
        else
        {
            userStats.CurrentStreak = currentStreak;
            userStats.BestStreak = Math.Max(userStats.BestStreak, currentStreak);
            userStats.LastPracticeDate = today;
            userStats.TotalLearningTimeSeconds = (long)sessions.Sum(s => (s.CompletedAt - s.StartedAt).TotalSeconds);
            userStats.TotalSessions = sessions.Count;
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

    private async Task CheckAndUnlockAchievementsAsync(int userId, TrainingSession session, CancellationToken ct)
    {
        var existingAchievementsList = await _context.UserAchievements
            .Where(a => a.UserId == userId)
            .Select(a => a.AchievementId)
            .ToListAsync(ct);

        var existingAchievements = existingAchievementsList.ToHashSet();

        var newAchievements = new List<UserAchievement>();

        // Проверка достижений по словам
        var learnedWords = await _context.LearningProgresses
            .CountAsync(p => p.UserId == userId && p.KnowledgeLevel >= 4, ct);

        CheckWordAchievements(learnedWords, existingAchievements, newAchievements, userId);

        // Проверка достижений по streak
        var userStats = await _context.UserStats.FindAsync(new object[] { userId }, ct);
        if (userStats != null)
        {
            CheckStreakAchievements(userStats.CurrentStreak, existingAchievements, newAchievements, userId);
        }

        // Проверка идеальной сессии
        if (session.WrongAnswers == 0 && session.WordsReviewed >= 10 && 
            !existingAchievements.Contains("perfect_session"))
        {
            newAchievements.Add(new UserAchievement
            {
                UserId = userId,
                AchievementId = "perfect_session",
                UnlockedAt = DateTime.UtcNow
            });
        }

        if (newAchievements.Count > 0)
        {
            _context.UserAchievements.AddRange(newAchievements);
            await _context.SaveChangesAsync(ct);
            
            _logger.LogInformation("User {UserId} unlocked {Count} new achievements", 
                userId, newAchievements.Count);
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
        var grouped = progresses
            .GroupBy(p => p.KnowledgeLevel)
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
        var allUserStats = await _context.LearningProgresses
            .GroupBy(p => p.UserId)
            .Select(g => new { UserId = g.Key, LearnedCount = g.Count(p => p.KnowledgeLevel >= 4) })
            .OrderByDescending(x => x.LearnedCount)
            .ToListAsync(ct);

        var totalUsers = allUserStats.Count;
        var userPosition = allUserStats.FindIndex(x => x.UserId == userId);

        if (userPosition == -1) return null;

        return new LeaderboardPosition
        {
            GlobalRank = userPosition + 1,
            TotalUsers = totalUsers,
            Percentile = totalUsers > 0 ? (1 - (double)userPosition / totalUsers) * 100 : 0,
            WeeklyRank = userPosition + 1, // Упрощённо - то же что и общий
            WeeklyPoints = allUserStats[userPosition].LearnedCount
        };
    }
}
