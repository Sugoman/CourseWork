using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace LearningTrainerShared.Models
{
    public class SettingsModel
    {
        public double BaseFontSize { get; set; } = 14;
        public string Theme { get; set; } = "Light";

        public string AccentColor { get; set; }
        public string BackgroundColor { get; set; }
        public string TextColor { get; set; }
    }
}
