using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace LearningTrainerShared.Models
{
    public class UpdateProgressRequest
    {
        public int WordId { get; set; }
        public ResponseQuality Quality { get; set; }

        /// <summary>
        /// Время ответа в миллисекундах (от показа слова до нажатия кнопки).
        /// Если передано — используется для автокоррекции качества (§1.3 LEARNING_IMPROVEMENTS).
        /// </summary>
        public int? ResponseTimeMs { get; set; }

        /// <summary>
        /// Режим упражнения (§5.1 LEARNING_IMPROVEMENTS). Используется для расчёта XP.
        /// flashcard=10, mcq=10, typing=15, listening=20, matching=10, cloze=15.
        /// </summary>
        public string? ExerciseMode { get; set; }
    }

    public class SetDailyGoalRequest
    {
        public int Goal { get; set; }
    }

    public class SaveNoteRequest
    {
        public string? Note { get; set; }
    }

    public enum ResponseQuality
    {
        Again,  // 0
        Hard,   // 1
        Good,   // 2
        Easy    // 3
    }
}
