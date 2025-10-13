using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace EnglishLearingTrainer.Models
{
    public class Dictionary
    {
        public int Id { get; set; }
        public string Name { get; set; }
        public string LanguageFrom { get; set; }
        public string LanguageTo { get; set; }
        public int WordCount { get; set; }
        public string Description { get; set; }
    }
}
