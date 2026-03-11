namespace Ingat.AI.Services;

/// <summary>
/// Шаблоны промптов для перевода и генерации примеров.
/// Системный промпт задаёт роль, пользовательский — конкретную задачу.
/// </summary>
public static class PromptTemplates
{
    public const string TranslateSystem =
        "You are a professional translator specializing in precise, context-aware translations. You ALWAYS respect the specified part of speech. Every word in your response (translation and alternatives) MUST be in the target language ONLY. You respond ONLY with valid JSON, no markdown, no explanation.";

    public static string TranslateUser(string word, string from, string to, string? context, string? partOfSpeech) =>
        $$"""
        Translate the word from {{from}} to {{to}}.
        Word: "{{word}}"
        {{(partOfSpeech != null ? $"CRITICAL: The word is used STRICTLY as a {partOfSpeech}. Translate ONLY the {partOfSpeech} meaning.\nExamples of part-of-speech disambiguation:\n- \"book\" as a noun = \"книга\", but as a verb = \"забронировать\" (NOT \"писать книгу\")\n- \"fly\" as a noun = \"муха\", but as a verb = \"летать\" (NOT \"самолёт\")\n- \"train\" as a noun = \"поезд\", but as a verb = \"тренировать\" (NOT \"ездить на поезде\")\n- \"run\" as a noun = \"забег/пробежка\", but as a verb = \"бежать\"\n- \"present\" as a noun = \"подарок\", but as an adjective = \"присутствующий/нынешний\"\n- \"duck\" as a noun = \"утка\", but as a verb = \"пригнуться/уклониться\"\nDo NOT confuse meanings from other parts of speech." : "")}}
        {{(context != null ? $"Context: \"{context}\"" : "")}}

        RULES:
        1. The "translation" field must contain ONLY the translated word or phrase in {{to}}. No numbers, indices, abbreviations, or explanations.
        2. The "alternatives" field must contain 1-3 SYNONYMS of the translation, also in {{to}} only.
        3. Alternatives must NOT be antonyms, must NOT be in {{from}} or any other language, and must NOT repeat the main translation.
        4. ALL text in "translation" and "alternatives" must be written ENTIRELY in {{to}}. Do not mix scripts or languages.
        Respond with JSON:
        {"translation": "main translation", "alternatives": ["alt1", "alt2"]}
        """;

    public const string ExampleSystem =
        "You are a language tutor who creates example sentences precisely matched to the student's CEFR level. You respond ONLY with a single valid JSON object. No markdown, no code blocks, no explanation before or after the JSON.";

    public static string ExampleUser(string word, string language, string targetLanguage, int count,
        string? partOfSpeech, string? languageLevel) =>
        $$"""
        Create exactly {{count}} short example sentence(s) using the word "{{word}}" in {{language}}.
        {{(partOfSpeech != null ? $"CRITICAL: The word \"{{word}}\" must be used ONLY as a {partOfSpeech} in every sentence.\nFor example, if the word is \"run\" and the part of speech is \"noun\", use it as \"a run\" (забег), NOT as a verb \"to run\".\nIf the word is \"book\" and the part of speech is \"verb\", use it as \"to book\" (забронировать), NOT as a noun.\nMake sure the word is grammatically used as a {partOfSpeech} in the sentence." : "")}}
        {{CefrInstruction(languageLevel)}}
        Translate each sentence to {{targetLanguage}}. The translation must be a complete, natural sentence — do not leave any words untranslated.
        You MUST return exactly {{count}} examples.
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

    // === Phase 4: Diverse Grammar Exercises (§17.3 LEARNING_IMPROVEMENTS) ===

    public const string GenerateTypedExercisesSystem =
        "You are an expert language teacher who creates diverse grammar exercises of a specific type. You respond ONLY with a single valid JSON object. No markdown, no code blocks, no explanation.";

    /// <summary>
    /// Генерирует грамматические упражнения указанного типа (§17.4 LEARNING_IMPROVEMENTS).
    /// </summary>
    public static string GenerateTypedExercisesUser(string ruleTitle, string ruleContent,
        string language, string targetLanguage, string exerciseType, int count, int difficultyTier) =>
        $$"""
        Create {{count}} grammar exercises of type "{{exerciseType}}" for the rule: "{{ruleTitle}}".
        Difficulty tier: {{difficultyTier}} (1=basic, 2=intermediate, 3=advanced).

        Rule description:
        {{ruleContent[..Math.Min(ruleContent.Length, 1000)]}}

        {{GetExerciseTypeInstructions(exerciseType)}}

        Language: {{language}}. Explanations in: {{targetLanguage}}.
        Return ONLY valid JSON matching the schema for type "{{exerciseType}}".
        """;

    private static string GetExerciseTypeInstructions(string exerciseType) => exerciseType switch
    {
        "transformation" => """
            EXERCISE TYPE: Sentence Transformation
            The student must rewrite a sentence according to instructions (change tense, voice, form).

            Return JSON:
            {"exercises": [{"question": "Rewrite in Past Simple: 'She goes to school every day.'", "correctAnswer": "She went to school every day.", "alternativeAnswers": ["She went to school each day."], "explanation": "Past Simple of 'go' is 'went'", "difficultyTier": 1}]}
            """,
        "error_correction" => """
            EXERCISE TYPE: Error Correction
            The student must find and correct the grammar error in the sentence.

            Return JSON:
            {"exercises": [{"question": "Find and correct the error:", "incorrectSentence": "She don't like coffee.", "correctAnswer": "She doesn't like coffee.", "alternativeAnswers": [], "explanation": "3rd person singular uses 'doesn't', not 'don't'", "difficultyTier": 1}]}
            """,
        "word_order" => """
            EXERCISE TYPE: Word Order
            The student must arrange shuffled words into a correct sentence.

            Return JSON:
            {"exercises": [{"question": "Arrange the words into a correct sentence:", "shuffledWords": ["been", "has", "she", "to", "Paris", "never"], "correctAnswer": "She has never been to Paris.", "alternativeAnswers": [], "explanation": "Present Perfect: Subject + has/have + never + past participle", "difficultyTier": 1}]}
            """,
        "translation" => """
            EXERCISE TYPE: Translation Challenge
            The student must translate a sentence applying the grammar rule.

            Return JSON:
            {"exercises": [{"question": "Translate: 'Если бы я был богат, я бы путешествовал.'", "correctAnswer": "If I were rich, I would travel.", "alternativeAnswers": ["If I was rich, I would travel."], "explanation": "Second conditional: If + past simple, would + infinitive", "difficultyTier": 2}]}
            """,
        "matching" => """
            EXERCISE TYPE: Matching
            Create pairs that the student must match (sentence halves, forms, etc.).

            Return JSON:
            {"exercises": [{"question": "Match the sentence halves:", "options": ["If I had money...|...I would buy a car.", "If I had had money...|...I would have bought a car.", "If I have money...|...I will buy a car."], "correctAnswer": "", "explanation": "Conditionals: 2nd (would+inf), 3rd (would have+pp), 1st (will+inf)", "difficultyTier": 2}]}
            """,
        _ => """
            EXERCISE TYPE: Fill-in-the-blank (MCQ)
            Standard multiple choice exercise with 4 options.

            Return JSON:
            {"exercises": [{"question": "She ___ to school every day.", "options": ["go", "goes", "going", "gone"], "correctIndex": 1, "explanation": "Present Simple, 3rd person singular", "difficultyTier": 1}]}
            """
    };

    public const string ExplainMistakeSystem =
        """You are a concise bilingual tutor. Respond ONLY with valid JSON. No markdown. Never invent words.""";

    public static string ExplainMistakeUser(string word, string userAnswer, string correctAnswer,
        string? context, string language, string targetLanguage) =>
        $$"""
        Word: "{{word}}" ({{language}}). Student answered: "{{userAnswer}}". Correct: "{{correctAnswer}}".
        {{(context != null ? $"Context: \"{context}\"" : "")}}

        LANGUAGE: Write BOTH fields ONLY in {{targetLanguage}} + {{language}}. No other languages allowed.
        Keep {{language}} words intact — never split them with spaces (write "incertidumbre", NOT "Ин certidumbre").

        "explanation" — TWO sentences:
        1) What {{word}} means. Use a synonym or description — do NOT just repeat the translation word.
        2) Вы выбрали «{{userAnswer}}» — это совсем другое: [what domain/concept the wrong answer belongs to, 5–12 words].

        "tip" — one example phrase using {{word}} in {{language}} + translation.
        Must give NEW context — do NOT repeat the translation from "explanation".

        Example (do NOT copy — write original for "{{word}}"):
        {"explanation": "Acontecimiento означает важный случай или происшествие. Вы выбрали «преодолевать» — это совсем другое: superar описывает победу над трудностью, а не сам свершившийся факт.", "tip": "El acontecimiento marcó un punto de inflexión — Этот случай стал поворотным моментом."}

        FORBIDDEN:
        - TAUTOLOGY: never define a word using itself ("событие — это событие")
        - Do NOT repeat the {{targetLanguage}} translation more than once across both fields
        - NEVER switch to Chinese, Japanese, Arabic or any language other than {{targetLanguage}} and {{language}}
        - Do NOT split {{language}} words with spaces ("incertidumbre" — one word, not two)
        - Do NOT invent words or transliterate between languages
        - For Arabic/Chinese/Japanese/Korean source words: use Latin transliteration

        Return ONLY: {"explanation": "...", "tip": "..."}
        """;

    public const string MnemonicSystem =
        """You are a bilingual vocabulary tutor. Respond ONLY with valid JSON. No markdown. Never invent words. Never mix scripts.""";

    public static string MnemonicUser(string word, string translation, string language, string targetLanguage) =>
        $$"""
        Memorize: "{{word}}" ({{language}}) = "{{translation}}" ({{targetLanguage}}).

        LANGUAGE: Write ALL fields ONLY in {{targetLanguage}} + {{language}}. No other languages allowed.
        Keep {{language}} words intact — never split them with spaces.

        "mnemonic": One sentence. Answer the question: WHO uses this word, or IN WHAT SITUATION? Name a specific context (a doctor diagnosing, a journalist writing, a teacher explaining, a politician debating, a tourist asking, etc.).
        "etymology": Real Latin/Greek/Arabic root if it exists. null if unknown. NEVER invent.
        "association": One phrase in {{language}} with {{targetLanguage}} translation.

        IMPORTANT: Each word must have its OWN unique context. Do NOT reuse the same domain (e.g. "спортивные новости") for different words.

        Example (do NOT copy — write original for "{{word}}"):
        {"mnemonic": "Desafío используют тренеры, когда ставят команде сложную задачу на тренировке.", "etymology": "От латинского diffidare (отказывать в доверии), через испанское desafiar.", "association": "Acepto el desafío — Я принимаю вызов."}

        FORBIDDEN:
        - TAUTOLOGY: never define a word using itself ("событие — это событие")
        - Do NOT repeat the {{targetLanguage}} translation more than twice
        - NEVER switch to Chinese, Japanese, Arabic or any language other than {{targetLanguage}} and {{language}}
        - Do NOT split {{language}} words with spaces
        - Do NOT invent words or create fake phonetic links
        - Do NOT start with "Представьте" / "Imagine"
        - Do NOT use vague phrases like "где-то и когда-то", "что-то важное"
        - Do NOT use "это как..." metaphors
        - For Arabic/Chinese/Japanese/Korean source words: use Latin transliteration

        Return ONLY: {"mnemonic": "...", "etymology": "...", "association": "..."}
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
