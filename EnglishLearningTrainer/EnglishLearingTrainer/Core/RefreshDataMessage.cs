using EnglishLearningTrainer.Core;

namespace EnglishLearningTrainer.Core
{
    public class RefreshDataMessage
    {
        // Можно добавить свойства для указания, какие именно данные нужно обновить
        public string DataType { get; set; } // "Dictionaries", "Rules", "All"

        public RefreshDataMessage()
        {
            DataType = "All";
        }

        public RefreshDataMessage(string dataType)
        {
            DataType = dataType;
        }
    }
}