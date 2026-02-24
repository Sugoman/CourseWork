namespace Ingat.AI.Services;

/// <summary>
/// Абстракция над LLM-рантаймом (Ollama, ONNX, OpenAI и т.д.).
/// </summary>
public interface IAiProvider
{
    /// <summary>
    /// Отправляет системный + пользовательский промпт модели и возвращает текстовый ответ.
    /// </summary>
    Task<string> CompleteAsync(string systemPrompt, string userPrompt, CancellationToken ct = default);
}
