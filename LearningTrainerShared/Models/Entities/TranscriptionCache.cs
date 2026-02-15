using System.ComponentModel.DataAnnotations;

namespace LearningTrainerShared.Models;

/// <summary>
/// Кэш транскрипций — хранит результат обращения к внешнему API,
/// чтобы не запрашивать одно и то же слово повторно.
/// </summary>
public class TranscriptionCache
{
    [Key]
    public int Id { get; set; }

    /// <summary>
    /// Слово в нижнем регистре (ключ поиска).
    /// </summary>
    [Required]
    [MaxLength(100)]
    public string WordLower { get; set; } = "";

    /// <summary>
    /// Транскрипция, полученная от внешнего API. null — слово не найдено в API.
    /// </summary>
    [MaxLength(200)]
    public string? Transcription { get; set; }

    /// <summary>
    /// Когда запись была создана.
    /// </summary>
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
}
