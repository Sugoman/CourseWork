namespace Ingat.AI.Services;

/// <summary>
/// Абстракция над LLM-рантаймом (Ollama, ONNX, OpenAI и т.д.).
/// </summary>
public interface IAiProvider
{
    /// <summary>
    /// Отправляет системный + пользовательский промпт модели и возвращает текстовый ответ.
    /// </summary>
    /// <param name="maxTokens">Максимальное количество токенов в ответе (num_predict). null = значение по умолчанию.</param>
    Task<string> CompleteAsync(string systemPrompt, string userPrompt, CancellationToken ct = default, int? maxTokens = null);
}
