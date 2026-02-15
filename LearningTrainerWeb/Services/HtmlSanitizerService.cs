using Ganss.Xss;
using LearningTrainerShared.Services;

namespace LearningTrainerWeb.Services;

/// <summary>
/// Сервис для санитизации HTML контента для защиты от XSS-атак.
/// </summary>
public interface IHtmlSanitizerService
{
    /// <summary>
    /// Очищает HTML от потенциально опасных элементов и атрибутов.
    /// </summary>
    string Sanitize(string? html);
}

public class HtmlSanitizerService : IHtmlSanitizerService
{
    private readonly HtmlSanitizer _sanitizer = SharedSanitizerFactory.Create();

    public string Sanitize(string? html)
    {
        if (string.IsNullOrEmpty(html))
            return string.Empty;

        return _sanitizer.Sanitize(html);
    }
}
