using EnglishLearningTrainer.Models;

namespace EnglishLearningTrainer.Core
{
    // Сообщение о добавлении нового правила
    public class RuleAddedMessage
    {
        public Rule Rule { get; }
        public RuleAddedMessage(Rule rule) => Rule = rule;
    }

    // Сообщение о добавлении нового словаря
    public class DictionaryAddedMessage
    {
        public Dictionary Dictionary { get; }
        public DictionaryAddedMessage(Dictionary dictionary) => Dictionary = dictionary;
    }

    // Сообщение о добавлении нового слова
    public class WordAddedMessage
    {
        public Word Word { get; }
        public int DictionaryId { get; }
        public WordAddedMessage(Word word, int dictionaryId)
        {
            Word = word;
            DictionaryId = dictionaryId;
        }
    }
}