using LearningTrainerShared.Models; 
using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace LearningTrainer.Services
{
    public class SessionService
    {
        private readonly string _sessionFilePath = "user_session.dat";
        private readonly string _lastUserLoginPath = "last_user.txt";

        public void SaveSession(UserSessionDto session)
        {
            var json = JsonSerializer.Serialize(session);
            var plainBytes = Encoding.UTF8.GetBytes(json);
            var protectedBytes = ProtectedData.Protect(plainBytes, null, DataProtectionScope.CurrentUser);
            File.WriteAllBytes(_sessionFilePath, protectedBytes);
            File.WriteAllText(_lastUserLoginPath, session.UserLogin);
        }

        public UserSessionDto? LoadSession()
        {
            if (!File.Exists(_sessionFilePath))
                return null;

            try
            {
                var protectedBytes = File.ReadAllBytes(_sessionFilePath);
                var plainBytes = ProtectedData.Unprotect(protectedBytes, null, DataProtectionScope.CurrentUser);
                var json = Encoding.UTF8.GetString(plainBytes);
                return JsonSerializer.Deserialize<UserSessionDto>(json);
            }
            catch (CryptographicException)
            {
                // Data was protected by another user or corrupted — clear it
                ClearSession();
                return null;
            }
        }

        public string? LoadLastUserLogin()
        {
            if (File.Exists(_lastUserLoginPath))
            {
                return File.ReadAllText(_lastUserLoginPath);
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
