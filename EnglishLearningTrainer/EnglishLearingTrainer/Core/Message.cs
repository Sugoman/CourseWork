using LearningTrainerShared.Models;

namespace LearningTrainer.Core
{
    public class RuleAddedMessage
    {
        public Rule Rule { get; }
        public RuleAddedMessage(Rule rule) => Rule = rule;
    }
    public class DictionaryAddedMessage
    {
        public Dictionary Dictionary { get; }
        public DictionaryAddedMessage(Dictionary dictionary) => Dictionary = dictionary;
    }

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

    public class DictionaryDeletedMessage
    {
        public int DictionaryId { get; }
        public DictionaryDeletedMessage(int dictionaryId) => DictionaryId = dictionaryId;
    }
}