namespace LearningTrainerShared.Services;

/// <summary>
/// Тип микро-цели внутри тренировочной сессии (§1.6 LEARNING_IMPROVEMENTS).
/// </summary>
public enum MicroGoalType
{
    HotStreak,
    Blitz,
    PerfectRound,
    Comeback,
    Unstoppable,
    FirstBlood
}

/// <summary>
/// Достигнутая микро-цель с описанием и бонусным XP.
/// </summary>
public sealed class MicroGoalReward
{
    public MicroGoalType Type { get; init; }
    public string Icon { get; init; } = "";
    public string Title { get; init; } = "";
    public string Description { get; init; } = "";
    public int BonusXp { get; init; }
}

/// <summary>
/// Отслеживает микро-цели в реальном времени внутри тренировочной сессии (§1.6 LEARNING_IMPROVEMENTS).
/// Полностью клиентская логика — без изменений в БД.
/// </summary>
public sealed class MicroGoalTracker
{
    private int _consecutiveCorrect;
    private int _totalCorrect;
    private int _totalWrong;
    private int _totalAnswers;
    private int _correctAfterError;
    private bool _hadError;
    private DateTime _sessionStart;
    private readonly HashSet<MicroGoalType> _achieved = new();

    public MicroGoalTracker()
    {
        _sessionStart = DateTime.UtcNow;
    }

    /// <summary>
    /// Общее количество бонусных XP, набранных через микро-цели за сессию.
    /// </summary>
    public int TotalBonusXp { get; private set; }

    /// <summary>
    /// Текущая серия правильных ответов.
    /// </summary>
    public int ConsecutiveCorrect => _consecutiveCorrect;

    /// <summary>
    /// Сбрасывает трекер для новой сессии.
    /// </summary>
    public void Reset()
    {
        _consecutiveCorrect = 0;
        _totalCorrect = 0;
        _totalWrong = 0;
        _totalAnswers = 0;
        _correctAfterError = 0;
        _hadError = false;
        _sessionStart = DateTime.UtcNow;
        _achieved.Clear();
        TotalBonusXp = 0;
    }

    /// <summary>
    /// Регистрирует ответ и возвращает список только что достигнутых микро-целей.
    /// </summary>
    public List<MicroGoalReward> RecordAnswer(bool wasCorrect)
    {
        _totalAnswers++;
        var rewards = new List<MicroGoalReward>();

        if (wasCorrect)
        {
            _totalCorrect++;
            _consecutiveCorrect++;

            if (_hadError)
                _correctAfterError++;
            else
                _correctAfterError = 0;

            // FirstBlood: первый правильный ответ сессии
            CheckGoal(rewards, MicroGoalType.FirstBlood,
                _totalCorrect == 1 && _totalWrong == 0,
                "🎯", "Первая кровь!", "Первый правильный ответ сессии", 3);

            // HotStreak: 5 подряд правильно
            CheckGoal(rewards, MicroGoalType.HotStreak,
                _consecutiveCorrect == 5,
                "🔥", "Серия 5!", "5 правильных ответов подряд", 5);

            // Unstoppable: 10 подряд правильно
            CheckGoal(rewards, MicroGoalType.Unstoppable,
                _consecutiveCorrect == 10,
                "⚡", "Неудержимый!", "10 правильных ответов подряд", 15);

            // Comeback: 3 правильных после ошибки
            CheckGoal(rewards, MicroGoalType.Comeback,
                _hadError && _correctAfterError == 3,
                "💪", "Камбэк!", "3 правильных ответа после ошибки", 10);

            // Blitz: 10 слов за 3 минуты
            var elapsed = DateTime.UtcNow - _sessionStart;
            CheckGoal(rewards, MicroGoalType.Blitz,
                _totalCorrect == 10 && elapsed.TotalMinutes <= 3,
                "⏱️", "Блиц!", "10 правильных за 3 минуты", 10);
        }
        else
        {
            _totalWrong++;
            _consecutiveCorrect = 0;
            _hadError = true;
            _correctAfterError = 0;
        }

        return rewards;
    }

    /// <summary>
    /// Проверяет микро-цель «Perfect Round» по завершении раунда.
    /// Вызывать после окончания сессии/раунда.
    /// </summary>
    public MicroGoalReward? CheckPerfectRound()
    {
        if (_achieved.Contains(MicroGoalType.PerfectRound))
            return null;

        if (_totalAnswers >= 5 && _totalWrong == 0)
        {
            _achieved.Add(MicroGoalType.PerfectRound);
            var reward = new MicroGoalReward
            {
                Type = MicroGoalType.PerfectRound,
                Icon = "🏆",
                Title = "Безупречный раунд!",
                Description = $"Все {_totalAnswers} ответов правильные",
                BonusXp = 20
            };
            TotalBonusXp += reward.BonusXp;
            return reward;
        }

        return null;
    }

    private void CheckGoal(List<MicroGoalReward> rewards, MicroGoalType type,
        bool condition, string icon, string title, string description, int bonusXp)
    {
        if (_achieved.Contains(type) || !condition)
            return;

        _achieved.Add(type);
        var reward = new MicroGoalReward
        {
            Type = type,
            Icon = icon,
            Title = title,
            Description = description,
            BonusXp = bonusXp
        };
        TotalBonusXp += reward.BonusXp;
        rewards.Add(reward);
    }
}
