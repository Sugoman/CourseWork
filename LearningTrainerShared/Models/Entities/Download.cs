using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace LearningTrainerShared.Models;

/// <summary>
/// «апись о скачивании контента пользователем
/// </summary>
public class Download
{
    [Key]
    public int Id { get; set; }

    [Required]
    public int UserId { get; set; }

    [Required]
    [MaxLength(20)]
    public string ContentType { get; set; } = ""; // "Dictionary" или "Rule"

    [Required]
    public int ContentId { get; set; }

    public DateTime DownloadedAt { get; set; } = DateTime.UtcNow;

    [ForeignKey(nameof(UserId))]
    public virtual User? User { get; set; }
}
