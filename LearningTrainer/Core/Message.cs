using LearningTrainerShared.Models;
using LearningTrainer.Services;

namespace LearningTrainer.Core
{
    public class RoleChangedMessage
    {
        public string NewToken { get; set; }
        public string NewRole { get; set; }
    }
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

    /// <summary>
    /// Сообщение для показа уведомления через EventAggregator
    /// </summary>
    public class ShowNotificationMessage
    {
        public NotificationType Type { get; }
        public string Title { get; }
        public string Message { get; }

        public ShowNotificationMessage(NotificationType type, string title, string message)
        {
            Type = type;
            Title = title;
            Message = message;
        }

        public static ShowNotificationMessage Success(string title, string message) 
            => new(NotificationType.Success, title, message);
        
        public static ShowNotificationMessage Error(string title, string message) 
            => new(NotificationType.Error, title, message);
        
        public static ShowNotificationMessage Info(string title, string message) 
            => new(NotificationType.Info, title, message);
    }
}
