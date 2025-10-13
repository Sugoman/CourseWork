using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace EnglishLearingTrainer.Models
{
    public class Word
    {
        public int Id { get; set; }
        public string OriginalWord { get; set; }
        public string Translation { get; set; }
        public string? ExampleSentence { get; set; }
    }
}
