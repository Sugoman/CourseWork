namespace LearningTrainerShared.Services
{
    using LearningTrainerShared.Models;

    /// <summary>
    /// Service encapsulating the SM-2 spaced repetition algorithm.
    /// Applies answer quality to a <see cref="LearningProgress"/> and computes the next review date.
    /// </summary>
    /// <remarks>
    /// SM-2 mapping:
    ///   ResponseQuality.Again = q1 (complete blackout)
    ///   ResponseQuality.Hard  = q3 (correct with serious difficulty)
    ///   ResponseQuality.Good  = q4 (correct after hesitation)
    ///   ResponseQuality.Easy  = q5 (perfect response)
    ///
    /// EaseFactor formula:  EF' = EF + (0.1 - (5-q) * (0.08 + (5-q) * 0.02))
    /// Interval schedule:   n=1 → 1 day, n=2 → 6 days, n>2 → I(n) = I(n-1) * EF
    /// If q &lt; 3 (Again) → reset repetition count to 0, start from 1 day.
    /// </remarks>
    public interface ISpacedRepetitionService
    {
        void ApplyAnswer(LearningProgress progress, ResponseQuality quality, int? responseTimeMs = null);
    }

    public class SpacedRepetitionService : ISpacedRepetitionService
    {
        private const double MinEaseFactor = 1.3;
        private const double DefaultEaseFactor = 2.5;

        /// <summary>
        /// Порог LapseCount, при котором слово автоматически помечается как leech.
        /// </summary>
        public const int LeechThreshold = 4;

        /// <summary>
        /// Порог времени (мс), ниже которого правильный ответ считается «Easy».
        /// </summary>
        private const int FastResponseMs = 2000;

        /// <summary>
        /// Порог времени (мс), выше которого правильный ответ понижается до «Hard».
        /// </summary>
        private const int SlowResponseMs = 10000;

        // Thread-safe random for jitter
        private static readonly Random _jitterRandom = new();

        public void ApplyAnswer(LearningProgress progress, ResponseQuality quality, int? responseTimeMs = null)
        {
            // Сохраняем время ответа (§1.3 LEARNING_IMPROVEMENTS)
            if (responseTimeMs.HasValue)
                progress.LastResponseTimeMs = responseTimeMs.Value;

            // Автокоррекция качества по времени ответа (только для правильных ответов)
            if (responseTimeMs.HasValue && quality >= ResponseQuality.Hard)
            {
                quality = AdjustQualityByResponseTime(quality, responseTimeMs.Value);
            }

            // Инициализация EaseFactor для legacy-данных (до миграции EaseFactor был 0)
            if (progress.EaseFactor < MinEaseFactor)
                progress.EaseFactor = DefaultEaseFactor;

            // Инициализация IntervalDays для legacy-данных
            if (progress.IntervalDays <= 0 && progress.KnowledgeLevel > 0)
                progress.IntervalDays = EstimateLegacyInterval(progress.KnowledgeLevel);

            progress.LastPracticed = DateTime.UtcNow;
            progress.TotalAttempts++;

            // Маппинг ResponseQuality → SM-2 quality (0–5)
            int q = MapToSm2Quality(quality);

            // Обновляем EaseFactor по формуле SM-2
            progress.EaseFactor = CalculateNewEaseFactor(progress.EaseFactor, q);

            if (q < 3) // Again → полный сброс
            {
                // Leech detection: считаем сброс только если слово уже было хотя бы частично выучено
                if (progress.KnowledgeLevel > 0)
                {
                    progress.LapseCount++;

                    // Автоматическая заморозка при достижении порога
                    if (progress.LapseCount >= LeechThreshold)
                    {
                        progress.IsSuspended = true;
                    }
                }

                progress.KnowledgeLevel = 0;
                progress.IntervalDays = 0;
                progress.NextReview = DateTime.UtcNow.AddMinutes(10);
            }
            else
            {
                progress.CorrectAnswers++;
                progress.KnowledgeLevel++;

                progress.IntervalDays = CalculateInterval(progress.KnowledgeLevel, progress.IntervalDays, progress.EaseFactor);

                // Для Easy (q=5) бонус: увеличиваем интервал на 30%
                if (quality == ResponseQuality.Easy)
                    progress.IntervalDays *= 1.3;

                // Fuzzy-интервалы (jitter ±10%) — предотвращает кластеризацию повторений (§2.2)
                double jitter;
                lock (_jitterRandom)
                {
                    jitter = 0.9 + _jitterRandom.NextDouble() * 0.2; // [0.9 .. 1.1]
                }
                progress.IntervalDays *= jitter;

                progress.NextReview = DateTime.UtcNow.AddDays(progress.IntervalDays);
            }
        }

        /// <summary>
        /// Автокоррекция качества ответа по времени (§1.3 LEARNING_IMPROVEMENTS).
        /// Правильно, но &gt;10 сек → понизить до Hard. Правильно за &lt;2 сек → повысить до Easy.
        /// </summary>
        private static ResponseQuality AdjustQualityByResponseTime(ResponseQuality quality, int responseTimeMs)
        {
            if (responseTimeMs < FastResponseMs && quality == ResponseQuality.Good)
                return ResponseQuality.Easy;

            if (responseTimeMs > SlowResponseMs && quality >= ResponseQuality.Good)
                return ResponseQuality.Hard;

            return quality;
        }

        /// <summary>
        /// Оценка интервала для legacy-записей, у которых IntervalDays не был заполнен.
        /// </summary>
        private static double EstimateLegacyInterval(int knowledgeLevel)
        {
            return knowledgeLevel switch
            {
                1 => 1,
                2 => 6,
                3 => 15,
                4 => 35,
                _ => Math.Max(1, knowledgeLevel * 10.0)
            };
        }

        /// <summary>
        /// Маппинг из 4-значной шкалы приложения в 6-значную шкалу SM-2 (0–5).
        /// </summary>
        private static int MapToSm2Quality(ResponseQuality quality)
        {
            return quality switch
            {
                ResponseQuality.Again => 1,  // Полный провал
                ResponseQuality.Hard  => 3,  // Правильно, но с большим трудом
                ResponseQuality.Good  => 4,  // Правильно после раздумий
                ResponseQuality.Easy  => 5,  // Безупречно
                _ => 3
            };
        }

        /// <summary>
        /// Формула SM-2 для пересчёта EaseFactor.
        /// EF' = EF + (0.1 - (5 - q) * (0.08 + (5 - q) * 0.02))
        /// </summary>
        private static double CalculateNewEaseFactor(double currentEf, int q)
        {
            double delta = 0.1 - (5 - q) * (0.08 + (5 - q) * 0.02);
            double newEf = currentEf + delta;
            return Math.Max(MinEaseFactor, newEf);
        }

        /// <summary>
        /// Расчёт интервала повторения по SM-2:
        ///   n = 1 → 1 день
        ///   n = 2 → 6 дней
        ///   n > 2 → I(n-1) * EF
        /// </summary>
        private static double CalculateInterval(int repetitionNumber, double previousInterval, double easeFactor)
        {
            return repetitionNumber switch
            {
                1 => 1,
                2 => 6,
                _ => previousInterval * easeFactor
            };
        }
    }
}
