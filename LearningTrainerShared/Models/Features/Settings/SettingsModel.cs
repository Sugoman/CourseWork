namespace LearningTrainerShared.Models
{
    public class SettingsModel
    {
        // === APPEARANCE ===
        public double BaseFontSize { get; set; } = 14;
        public string Theme { get; set; } = "Light";
        public string Language { get; set; } = "en";
        public string FontFamily { get; set; } = "Segoe UI";
        public bool EnableAnimations { get; set; } = true;

        // === NOTIFICATIONS ===
        public bool EnableNotifications { get; set; } = true;
        public int NotificationDurationSeconds { get; set; } = 4;

        // === LEARNING ===
        public int DailyGoal { get; set; } = 10;
        public bool EnableSoundEffects { get; set; } = false;
        public bool ShowTranscription { get; set; } = true;

        // === PRIVACY ===
        public bool KeepMeLoggedIn { get; set; } = false;
        public bool AutoSync { get; set; } = true;
    }
}
