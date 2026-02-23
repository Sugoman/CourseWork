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

        [JsonPropertyName("meanings")]
        public List<MeaningDto>? Meanings { get; set; }
    }

    public class MeaningDto
    {
        [JsonPropertyName("partOfSpeech")]
        public string? PartOfSpeech { get; set; }

        [JsonPropertyName("definitions")]
        public List<DefinitionDto>? Definitions { get; set; }
    }

    public class DefinitionDto
    {
        [JsonPropertyName("definition")]
        public string? Definition { get; set; }

        [JsonPropertyName("example")]
        public string? Example { get; set; }
    }
}
