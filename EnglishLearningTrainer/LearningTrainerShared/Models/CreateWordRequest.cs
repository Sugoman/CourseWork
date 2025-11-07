using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace LearningTrainerShared.Models
{
    public class CreateWordRequest
    {
        public string OriginalWord { get; set; }
        public string Translation { get; set; }
        public string Example { get; set; }
        public int DictionaryId { get; set; } 
    }
}
