namespace LearningAPI.Configuration;

/// <summary>
/// Конфигурация для health checks
/// </summary>
public class HealthCheckConfiguration
{
    public const string SectionName = "HealthCheck";

    /// <summary>
    /// Длительность кэширования результатов в секундах (для production)
    /// </summary>
    public int CacheDurationSeconds { get; set; } = 30;

    /// <summary>
    /// Пороговые значения для мониторинга
    /// </summary>
    public HealthCheckThresholds Thresholds { get; set; } = new();

    /// <summary>
    /// Внешние зависимости для проверки
    /// </summary>
    public List<ExternalDependencyConfig> ExternalDependencies { get; set; } = new();
}

/// <summary>
/// Пороговые значения для определения состояния системы
/// </summary>
public class HealthCheckThresholds
{
    /// <summary>
    /// Предупреждение при использовании памяти (MB)
    /// </summary>
    public long MemoryWarningMB { get; set; } = 500;

    /// <summary>
    /// Критическое использование памяти (MB)
    /// </summary>
    public long MemoryCriticalMB { get; set; } = 1000;

    /// <summary>
    /// Предупреждение при низком дисковом пространстве (GB)
    /// </summary>
    public long DiskSpaceWarningGB { get; set; } = 5;

    /// <summary>
    /// Критически низкое дисковое пространство (GB)
    /// </summary>
    public long DiskSpaceCriticalGB { get; set; } = 1;

    /// <summary>
    /// Предупреждение при медленном ответе (ms)
    /// </summary>
    public int ResponseTimeWarningMs { get; set; } = 1000;

    /// <summary>
    /// Критически медленный ответ (ms)
    /// </summary>
    public int ResponseTimeCriticalMs { get; set; } = 5000;
}

/// <summary>
/// Конфигурация внешней зависимости
/// </summary>
public class ExternalDependencyConfig
{
    /// <summary>
    /// Название сервиса
    /// </summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// URL для проверки здоровья
    /// </summary>
    public string Url { get; set; } = string.Empty;

    /// <summary>
    /// Таймаут в секундах
    /// </summary>
    public int TimeoutSeconds { get; set; } = 5;

    /// <summary>
    /// Включена ли проверка
    /// </summary>
    public bool Enabled { get; set; } = true;
}
