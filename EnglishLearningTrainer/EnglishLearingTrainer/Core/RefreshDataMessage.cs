using LearningTrainer.Core;

namespace LearningTrainer.Core
{
    public class RefreshDataMessage
    {
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