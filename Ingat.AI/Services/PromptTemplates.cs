namespace Ingat.AI.Services;

/// <summary>
/// Шаблоны промптов для перевода и генерации примеров.
/// Системный промпт задаёт роль, пользовательский — конкретную задачу.
/// </summary>
public static class PromptTemplates
{
    public const string TranslateSystem =
        "You are a professional translator specializing in precise, context-aware translations. You ALWAYS respect the specified part of speech. You respond ONLY with valid JSON, no markdown, no explanation.";

    public static string TranslateUser(string word, string from, string to, string? context, string? partOfSpeech) =>
        $$"""
        Translate the word from {{from}} to {{to}}.
        Word: "{{word}}"
        {{(partOfSpeech != null ? $"IMPORTANT: The word is used as a {partOfSpeech}. Translate ONLY the {partOfSpeech} meaning. For example, \"run\" as a noun = \"забег/пробежка\", but as a verb = \"бежать\". Do NOT mix up parts of speech." : "")}}
        {{(context != null ? $"Context: \"{context}\"" : "")}}

        Respond with JSON:
        {"translation": "main translation", "alternatives": ["alt1", "alt2"]}
        """;

    public const string ExampleSystem =
        "You are a language tutor who creates example sentences precisely matched to the student's CEFR level. You respond ONLY with a single valid JSON object. No markdown, no code blocks, no explanation before or after the JSON.";

    public static string ExampleUser(string word, string language, string targetLanguage, int count,
        string? partOfSpeech, string? languageLevel) =>
        $$"""
        Create {{count}} short example sentence(s) using the word "{{word}}" in {{language}}.
        {{(partOfSpeech != null ? $"IMPORTANT: Use the word ONLY as a {partOfSpeech} (not any other part of speech)." : "")}}
        {{CefrInstruction(languageLevel)}}
        Translate each sentence to {{targetLanguage}}.
        Return ONLY this exact JSON structure (always use an array even for 1 example):
        {"examples": [{"sentence": "example sentence here", "translation": "translation here"}]}
        """;

    public const string GenerateDictionarySystem =
        "You are an expert language tutor who creates vocabulary lists precisely matched to CEFR levels. You respond ONLY with a single valid JSON object. No markdown, no code blocks, no explanation.";

    public static string GenerateDictionaryUser(string topic, string sourceLang, string targetLang,
        string level, int wordCount) =>
        $$"""
        Generate a vocabulary list of {{wordCount}} {{sourceLang}} words related to the topic: "{{topic}}".
        {{CefrInstruction(level)}}
        For each word provide:
        - the word in {{sourceLang}}
        - translation to {{targetLang}}
        - part of speech (noun, verb, adjective, etc.)
        - a short example sentence in {{sourceLang}} appropriate for level {{level}}
        All words must be unique and directly related to the topic.
        Return ONLY this JSON (always use an array):
        {"words": [{"original": "word", "translation": "перевод", "partOfSpeech": "noun", "example": "example sentence"}]}
        """;

    public const string BatchTranslateSystem =
        "You are a professional translator. You respond ONLY with valid JSON, no markdown, no explanation.";

    public static string BatchTranslateUser(IEnumerable<string> words, string from, string to)
    {
        var wordList = string.Join("\n", words.Select((w, i) => $"{i + 1}. {w}"));
        return $$"""
        Translate each of the following words from {{from}} to {{to}}.
        Words:
        {{wordList}}

        Respond with JSON:
        {"translations": [{"word": "original", "translation": "translated", "alternatives": ["alt1"]}]}
        Return one entry per word in the same order.
        """;
    }

    // === Phase 3: New AI Features ===

    public const string GenerateExercisesSystem =
        "You are an expert language teacher who creates grammar exercises. You respond ONLY with a single valid JSON object. No markdown, no code blocks, no explanation.";

    public static string GenerateExercisesUser(string ruleTitle, string ruleContent, string language,
        string targetLanguage, int count, string? level) =>
        $$"""
        Create {{count}} fill-in-the-blank grammar exercises for the rule: "{{ruleTitle}}".

        Rule description:
        {{ruleContent[..Math.Min(ruleContent.Length, 1000)]}}

        Language: {{language}}.
        {{(level != null ? $"Target CEFR level: {level}." : "")}}
        Each exercise must have:
        - A sentence with a blank (use ___ for the blank)
        - Exactly 4 answer options
        - The index (0-based) of the correct option
        - A brief explanation in {{targetLanguage}} of why the answer is correct

        Return ONLY this JSON:
        {"exercises": [{"question": "She ___ to school every day.", "options": ["go", "goes", "going", "gone"], "correctIndex": 1, "explanation": "Present Simple, 3rd person singular → -es"}]}
        """;

    public const string ExplainMistakeSystem =
        "You are a patient language tutor. You respond ONLY with a single valid JSON object. No markdown, no code blocks, no explanation.";

    public static string ExplainMistakeUser(string word, string userAnswer, string correctAnswer,
        string? context, string language, string targetLanguage) =>
        $$"""
        A student was asked to translate the {{language}} word "{{word}}" and answered "{{userAnswer}}", but the correct answer is "{{correctAnswer}}".
        {{(context != null ? $"Context: \"{context}\"" : "")}}

        Provide a brief, encouraging explanation in {{targetLanguage}} (2-3 sentences max):
        1. Why "{{correctAnswer}}" is correct
        2. Why "{{userAnswer}}" is wrong or incomplete
        3. A quick tip to remember the correct answer

        Return ONLY this JSON:
        {"explanation": "brief explanation here", "tip": "memory tip here"}
        """;

    public const string MnemonicSystem =
        "You are a creative language learning assistant who helps students memorize words. You respond ONLY with a single valid JSON object. No markdown, no code blocks, no explanation.";

    public static string MnemonicUser(string word, string translation, string language, string targetLanguage) =>
        $$"""
        Help memorize the {{language}} word "{{word}}" (meaning: "{{translation}}" in {{targetLanguage}}).

        Create a mnemonic aid in {{targetLanguage}} that includes:
        1. A vivid association or mental image linking the sound/spelling of "{{word}}" to its meaning "{{translation}}"
        2. Brief etymology or word origin (if interesting and helpful)
        3. A memorable phrase or sentence connecting the word to something familiar

        Keep it short and memorable (2-3 sentences for association).

        Return ONLY this JSON:
        {"mnemonic": "vivid association here", "etymology": "word origin here or null", "association": "memorable phrase here"}
        """;

    public const string DetectLanguageSystem =
        "You are a multilingual linguist. You respond ONLY with a single valid JSON object. No markdown, no code blocks, no explanation.";

    public static string DetectLanguageUser(string text) =>
        $$"""
        Detect the language of the following text: "{{text[..Math.Min(text.Length, 500)]}}"

        Respond with the language name in English and your confidence (0.0 to 1.0).
        Return ONLY this JSON:
        {"language": "English", "confidence": 0.95}
        """;

    public const string ExtractWordsSystem =
        "You are a language teacher who identifies key vocabulary in texts. You respond ONLY with a single valid JSON object. No markdown, no code blocks, no explanation.";

    public static string ExtractWordsUser(string text, string language, string targetLanguage,
        int maxWords, string? level) =>
        $$"""
        Extract up to {{maxWords}} key vocabulary words from the following {{language}} text that would be useful for a language learner to study.
        {{(level != null ? $"Focus on words at or above CEFR level {level} (skip very common words like articles, pronouns)." : "Focus on interesting, useful vocabulary (skip very common words).")}}

        Text:
        {{text[..Math.Min(text.Length, 2000)]}}

        For each word provide:
        - The word in its base/dictionary form in {{language}}
        - Translation to {{targetLanguage}}
        - Part of speech
        - The sentence fragment from the text where it appears

        Return ONLY this JSON:
        {"words": [{"original": "word", "translation": "перевод", "partOfSpeech": "noun", "context": "sentence fragment with the word"}]}
        """;

    /// <summary>
    /// Generates explicit CEFR-level instructions with word count, grammar, and vocabulary constraints.
    /// Helps the model produce genuinely level-appropriate content instead of generic sentences.
    /// </summary>
    private static string CefrInstruction(string? level) => level?.ToUpperInvariant() switch
    {
        "A1" => """
            IMPORTANT — CEFR level A1 (Beginner):
            - Use ONLY the 500 most common words (I, you, have, go, like, eat, big, small, good, bad).
            - Maximum 6 words per sentence. Use present simple tense ONLY.
            - NO subordinate clauses, NO passive voice, NO idioms.
            - Example of A1 sentence: "I like cats." / "She is happy."
            """,
        "A2" => """
            IMPORTANT — CEFR level A2 (Elementary):
            - Use common everyday vocabulary (about 1000 most frequent words).
            - Maximum 10 words per sentence. Use present simple, past simple, can/want.
            - Simple compound sentences allowed (and, but, because).
            - Example of A2 sentence: "I went to the shop and bought some bread."
            """,
        "B1" => """
            IMPORTANT — CEFR level B1 (Intermediate):
            - Use intermediate vocabulary. Sentences of 8–15 words.
            - Use present perfect, conditionals (if), relative clauses (who, which, that).
            - Example of B1 sentence: "The book that I read last week was really interesting."
            """,
        "B2" => """
            IMPORTANT — CEFR level B2 (Upper-Intermediate):
            - Use varied vocabulary including abstract nouns and phrasal verbs.
            - Complex sentences with multiple clauses. Passive voice is OK.
            - Example of B2 sentence: "Despite the challenges, the project was completed ahead of schedule."
            """,
        "C1" => """
            IMPORTANT — CEFR level C1 (Advanced):
            - Use sophisticated vocabulary, idiomatic expressions, academic language.
            - Complex sentence structures with nuanced meaning.
            - Example of C1 sentence: "Having been subjected to extensive scrutiny, the proposal was eventually endorsed."
            """,
        "C2" => """
            IMPORTANT — CEFR level C2 (Mastery):
            - Use the full range of the language: rare words, literary expressions, technical terms.
            - Highly complex and nuanced sentences with subtle meaning distinctions.
            - Example of C2 sentence: "The inexorable march of technological innovation has engendered a paradigm shift."
            """,
        _ => ""
    };
}
