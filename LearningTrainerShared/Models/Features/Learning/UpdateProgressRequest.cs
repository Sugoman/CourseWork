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
    }

    public class SetDailyGoalRequest
    {
        public int Goal { get; set; }
    }

    public enum ResponseQuality
    {
        Again,  // 0
        Hard,   // 1
        Good,   // 2
        Easy    // 3
    }
}
