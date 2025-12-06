using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json.Serialization;
using System.Threading.Tasks;

namespace LearningTrainerShared.Models
{
    public class DictionaryApiEntryDto
    {
        [JsonPropertyName("phonetic")]
        public string? Phonetic { get; set; }

        [JsonPropertyName("phonetics")]
        public List<PhoneticDto>? Phonetics { get; set; }
    }
}
