namespace LearningTrainerShared.Services
{
    using LearningTrainerShared.Models;

    /// <summary>
    /// Service encapsulating the spaced repetition algorithm (simplified SM-2).
    /// Applies answer quality to a LearningProgress and computes the next review date.
    /// </summary>
    public interface ISpacedRepetitionService
    {
        void ApplyAnswer(LearningProgress progress, ResponseQuality quality);
    }

    public class SpacedRepetitionService : ISpacedRepetitionService
    {
        private const int MaxKnowledgeLevel = 5;

        public void ApplyAnswer(LearningProgress progress, ResponseQuality quality)
        {
            progress.LastPracticed = DateTime.UtcNow;
            progress.TotalAttempts++;

            switch (quality)
            {
                case ResponseQuality.Again:
                    progress.KnowledgeLevel = 0;
                    progress.NextReview = DateTime.UtcNow.AddMinutes(5);
                    break;

                case ResponseQuality.Hard:
                    progress.CorrectAnswers++;
                    progress.NextReview = DateTime.UtcNow.AddDays(1);
                    break;

                case ResponseQuality.Good:
                    progress.CorrectAnswers++;
                    if (progress.KnowledgeLevel < MaxKnowledgeLevel)
                        progress.KnowledgeLevel++;

                    progress.NextReview = GetNextReviewDate(progress.KnowledgeLevel, multiplier: 1.0);
                    break;

                case ResponseQuality.Easy:
                    progress.CorrectAnswers++;
                    progress.KnowledgeLevel = Math.Min(MaxKnowledgeLevel, progress.KnowledgeLevel + 2);

                    progress.NextReview = GetNextReviewDate(progress.KnowledgeLevel, multiplier: 1.5);
                    break;
            }
        }

        private static DateTime GetNextReviewDate(int knowledgeLevel, double multiplier)
        {
            var baseIntervalDays = knowledgeLevel switch
            {
                1 => 1,
                2 => 3,
                3 => 7,
                4 => 14,
                _ => 30
            };
            return DateTime.UtcNow.AddDays(baseIntervalDays * multiplier);
        }
    }
}
