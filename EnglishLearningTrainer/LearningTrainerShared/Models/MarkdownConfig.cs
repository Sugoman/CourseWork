using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace LearningTrainerShared.Models
{
    public class MarkdownConfig
    {
        public int FontSize { get; set; } = 16;
        public string AccentColor { get; set; } = "#00b8d4";
        public string BackgroundColor { get; set; } = "#ffffff";
        public string TextColor { get; set; } = "#000000";
        public string ParagraphColor { get; set; } = "#191919";
        public string CodeColor { get; set; } = "#111111";
        public string CodeBackgroundColor { get; set; } = "#bbbbbb";
       
    }
}
