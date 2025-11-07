using LearningTrainerShared.Models; 
using System.IO;
using System.Text.Json;

namespace LearningTrainer.Services
{
    public class SessionService
    {
        private readonly string _sessionFilePath = "user_session.json";

        public void SaveSession(UserSessionDto session)
        {
            var json = JsonSerializer.Serialize(session);
            File.WriteAllText(_sessionFilePath, json);
        }

        public UserSessionDto? LoadSession()
        {
            if (File.Exists(_sessionFilePath))
            {
                var json = File.ReadAllText(_sessionFilePath);
                return JsonSerializer.Deserialize<UserSessionDto>(json);
            }
            return null;
        }

        public void ClearSession()
        {
            if (File.Exists(_sessionFilePath))
            {
                File.Delete(_sessionFilePath);
            }
        }
    }
}