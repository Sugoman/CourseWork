namespace Ingat.AI.Services;

/// <summary>
/// Шаблоны промптов для перевода и генерации примеров.
/// Системный промпт задаёт роль, пользовательский — конкретную задачу.
/// </summary>
public static class PromptTemplates
{
    public const string TranslateSystem =
        "You are a professional translator. You respond ONLY with valid JSON, no markdown, no explanation.";

    public static string TranslateUser(string word, string from, string to, string? context, string? partOfSpeech) =>
        $$"""
        Translate the word from {{from}} to {{to}}.
        Word: "{{word}}"
        {{(partOfSpeech != null ? $"Part of speech: {partOfSpeech}" : "")}}
        {{(context != null ? $"Context: \"{context}\"" : "")}}

        Respond with JSON:
        {"translation": "main translation", "alternatives": ["alt1", "alt2"]}
        """;

    public const string ExampleSystem =
        "You are a language tutor. You respond ONLY with a single valid JSON object. No markdown, no code blocks, no explanation before or after the JSON.";

    public static string ExampleUser(string word, string language, string targetLanguage, int count,
        string? partOfSpeech, string? languageLevel) =>
        $$"""
        Create {{count}} short example sentence(s) using the word "{{word}}" in {{language}}.
        {{(partOfSpeech != null ? $"Use the word as a {partOfSpeech}." : "")}}
        {{(languageLevel != null ? $"Target the sentence complexity for CEFR level {languageLevel}. Use simple vocabulary and grammar for A1-A2, intermediate for B1-B2, and advanced for C1-C2." : "")}}
        Translate each sentence to {{targetLanguage}}.
        Return ONLY this exact JSON structure (always use an array even for 1 example):
        {"examples": [{"sentence": "example sentence here", "translation": "translation here"}]}
        """;

    public const string GenerateDictionarySystem =
        "You are an expert language tutor who creates vocabulary lists. You respond ONLY with a single valid JSON object. No markdown, no code blocks, no explanation.";

    public static string GenerateDictionaryUser(string topic, string sourceLang, string targetLang,
        string level, int wordCount) =>
        $$"""
        Generate a vocabulary list of {{wordCount}} {{sourceLang}} words related to the topic: "{{topic}}".
        Target CEFR level: {{level}}.
        For A1-A2: use common everyday words. For B1-B2: use intermediate vocabulary. For C1-C2: use advanced/academic words.
        For each word provide:
        - the word in {{sourceLang}}
        - translation to {{targetLang}}
        - part of speech (noun, verb, adjective, etc.)
        - a short example sentence in {{sourceLang}} appropriate for level {{level}}
        All words must be unique and directly related to the topic.
        Return ONLY this JSON (always use an array):
        {"words": [{"original": "word", "translation": "перевод", "partOfSpeech": "noun", "example": "example sentence"}]}
        """;
}
