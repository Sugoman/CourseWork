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

        CRITICAL — TOPIC MATCHING:
        Every exercise MUST test EXACTLY the grammar rule "{{ruleTitle}}" and nothing else.
        Do NOT use a different tense, aspect, or grammatical structure, even if it looks similar.
        Examples of WRONG topic matching:
        - Topic "Present Continuous" → sentence uses Present Perfect Continuous ("have been doing") — WRONG
        - Topic "Past Simple" → sentence uses Past Perfect ("had done") — WRONG
        - Topic "Present Simple" → sentence uses Present Continuous ("is doing") — WRONG
        - Topic "First Conditional" → sentence uses Second Conditional ("If I were...") — WRONG
        If the rule title specifies a particular tense/structure, use ONLY that exact tense/structure.

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
        "dictation" => """
            EXERCISE TYPE: Dictation
            The student will hear a sentence read aloud by TTS, then must type it from memory.
            Create natural, grammatically rich sentences that test the grammar rule.
            The "question" field should contain a short instruction (e.g. "Listen and write the sentence.").
            The "correctAnswer" field MUST contain the full sentence the student should type.
            Provide reasonable alternative answers that differ only in minor punctuation or acceptable spelling.

            Return JSON:
            {"exercises": [{"question": "Listen and write the sentence.", "correctAnswer": "She has been studying English for three years.", "alternativeAnswers": ["She has been studying English for 3 years."], "explanation": "Present Perfect Continuous: has/have + been + V-ing", "difficultyTier": 1}]}
            """,
        _ => """
            EXERCISE TYPE: Fill-in-the-blank (MCQ)
            Standard multiple choice exercise with exactly 4 options.

            CRITICAL RULES:
            1. The "question" field MUST contain a blank shown as "___" (three underscores) where the answer goes.
               NEVER put the correct answer into the sentence. The student must choose it from options.
               WRONG: "She went to school every day."  CORRECT: "She ___ to school every day."
            2. "options" MUST contain exactly 4 items. One of them is the correct answer.
            3. "correctIndex" is the 0-based index of the correct answer inside "options".
               The option at options[correctIndex] MUST be the word/phrase that fills the blank.
            4. All 4 options must be the same part of speech / form category (e.g. all verb forms, all prepositions).
               Wrong options must be plausible but grammatically incorrect in this sentence.
            5. The sentence with the blank replaced by options[correctIndex] MUST be grammatically correct.
            6. Each exercise must test the grammar rule "{{ruleTitle}}" — do not test unrelated grammar.
            7. For compound tenses (Continuous, Perfect, etc.) the blank and options MUST include
               the FULL verb phrase with the auxiliary verb.
               WRONG question: "She ___ a book now." with options ["read","reading","reads","readed"]
                 — "reading" alone is incomplete; the sentence "She reading a book now" is ungrammatical.
               CORRECT question: "She ___ a book now." with options ["is reading","reads","read","has read"]
                 — "She is reading a book now" is grammatically complete.
               Another valid approach: keep the auxiliary in the sentence and blank only the main verb:
               "She is ___ a book now." with options ["read","reading","reads","readed"]
            8. All 4 options MUST be unique. Never repeat the same word/phrase twice.

            Example (Past Simple):
            {"exercises": [{"question": "She ___ to school yesterday.", "options": ["go", "went", "going", "gone"], "correctIndex": 1, "explanation": "Past Simple of 'go' is 'went'", "difficultyTier": 1}]}

            Example (Present Continuous):
            {"exercises": [{"question": "They ___ football right now.", "options": ["are playing", "play", "played", "plays"], "correctIndex": 0, "explanation": "Present Continuous: am/is/are + V-ing", "difficultyTier": 1}]}
            """
    };

    public const string ValidateExerciseSystem =
        "You are a strict grammar exercise reviewer. You verify that exercises are logically correct, match the stated grammar topic, and have no contradictions. Respond ONLY with valid JSON. No markdown, no code blocks.";

    public static string ValidateExerciseBatch(string ruleTitle, string exerciseType, string exercisesJson) =>
        $$"""
        Verify this batch of "{{exerciseType}}" grammar exercises for the topic: "{{ruleTitle}}".

        Exercises JSON:
        {{exercisesJson}}

        For EACH exercise, check:
        1. TOPIC MATCHING (most important check): Does the sentence test EXACTLY the grammar topic "{{ruleTitle}}" and ONLY that topic?
           - If the topic is a specific tense (e.g. "Present Continuous"), the exercise MUST use that exact tense, NOT a similar one.
           - Mark as INVALID if the exercise uses a different tense/structure, even a closely related one.
           - Examples of mismatches that MUST be rejected:
             * Topic "Present Continuous" but sentence uses Present Perfect Continuous ("have been doing") → INVALID
             * Topic "Past Simple" but sentence uses Past Continuous ("was doing") → INVALID
             * Topic "Present Perfect" but sentence uses Present Perfect Continuous ("have been doing") → INVALID
             * Topic "Past Simple" but sentence uses Past Perfect ("had done") → INVALID
             * Topic "Will Future" but sentence uses "going to" future → INVALID
           - Analyze the actual verb forms in the sentence/answer, not just the time markers.
        2. Is the correct answer actually correct? (e.g. for MCQ: does options[correctIndex] fill the blank to make a grammatically correct sentence?)
        3. Are the wrong options actually wrong? (they must NOT also be correct in the given sentence)
        4. Is there any logical contradiction? (e.g. time marker says "last night" but the correct answer is present tense)
        5. For MCQ: does the blank "___" replace exactly the word being tested?
        6. For compound tenses (Continuous, Perfect, etc.): does the correct option include the auxiliary verb? E.g. "She ___ a book now" with correct answer "reading" is INVALID because "She reading a book now" is ungrammatical — the correct answer must be "is reading".
        7. Are all options unique? Duplicate options make the exercise invalid.

        Return ONLY a JSON array with one object per exercise, in the same order:
        [{"index": 0, "valid": true, "reason": ""}, {"index": 1, "valid": false, "reason": "Correct answer 'watching' is present continuous, not Past Simple as required by the topic"}]
        """;

    public const string ExplainMistakeSystem =
        """You are a concise bilingual tutor. Respond ONLY with valid JSON. No markdown. Never invent words.""";

    public static string ExplainMistakeUser(string word, string userAnswer, string correctAnswer,
        string? context, string language, string targetLanguage) =>
        $$"""
        Word: "{{word}}" ({{language}}). Student answered: "{{userAnswer}}". Correct: "{{correctAnswer}}".
        {{(context != null ? $"Context: \"{context}\"" : "")}}

        CRITICAL RULES:
        1. "explanation" MUST start with the word "{{word}}" (in {{language}}). Never replace it with a pronoun.
        2. "tip" MUST contain a phrase in {{language}} followed by " — " and its {{targetLanguage}} translation.
        3. Write in {{targetLanguage}} except for {{language}} words/phrases.

        "explanation" format:
        "{{word}} означает [synonym, not the bare translation]. Вы выбрали «{{userAnswer}}» — это совсем другое: [why wrong, 5–12 words]."

        "tip" format:
        "[phrase in {{language}} using {{word}}] — [translation in {{targetLanguage}}]."

        Example (write ORIGINAL for "{{word}}", do NOT copy):
        {"explanation": "Acontecimiento означает важный случай или происшествие. Вы выбрали «преодолевать» — это совсем другое: superar описывает победу над трудностью, а не сам факт.", "tip": "El acontecimiento marcó un punto de inflexión — Этот случай стал поворотным моментом."}

        FORBIDDEN:
        - Do NOT replace "{{word}}" with "это слово" or any pronoun
        - Do NOT omit the {{language}} phrase from "tip"
        - TAUTOLOGY: never define a word using itself
        - NEVER switch to Chinese, Japanese, Arabic or any other language

        Return ONLY: {"explanation": "...", "tip": "..."}
        """;

    public const string MnemonicSystem =
        """You are a bilingual vocabulary tutor who creates memorable associations. Respond ONLY with valid JSON. No markdown. Never invent words.""";

    public static string MnemonicUser(string word, string translation, string language, string targetLanguage) =>
        $$"""
        Help memorize: "{{word}}" ({{language}}) = "{{translation}}" ({{targetLanguage}}).

        CRITICAL RULES:
        1. "mnemonic" MUST start with "{{word}}" (the {{language}} word). Never replace it with a pronoun.
        2. "association" MUST contain a phrase in {{language}} followed by " — " and {{targetLanguage}} translation.
        3. Write in {{targetLanguage}} except for {{language}} words/phrases.

        "mnemonic": Start with "{{word}}". Then choose ONE approach:
        a) If {{word}} sounds similar to a {{targetLanguage}} word — point out the sound link.
        b) If there is a real etymology — explain the Latin/Greek root and how it connects to the meaning.
        c) Otherwise — give a vivid one-sentence image that links the sound or spelling of {{word}} to its meaning "{{translation}}".
        Do NOT just say "someone uses this word when...". Explain WHY it is easy to remember.

        "etymology": Real Latin/Greek/Arabic root if it exists. null if unknown. NEVER invent.

        "association": A short memorable phrase using {{word}} in {{language}}, then " — ", then {{targetLanguage}} translation.

        Example (write ORIGINAL for "{{word}}", do NOT copy):
        {"mnemonic": "Comer звучит как русское 'коморка' — маленькая комната, где едят, как кухня.", "etymology": "От латинского comedere (есть, поглощать).", "association": "Vamos a comer juntos — Давай поедим вместе."}

        FORBIDDEN:
        - Do NOT replace "{{word}}" with "это слово" or any pronoun
        - Do NOT omit the {{language}} phrase from "association"
        - Do NOT write generic sentences like "someone uses this word when..."
        - TAUTOLOGY: never define a word using itself
        - NEVER switch to Chinese, Japanese, Arabic or any other language
        - Do NOT invent fake sound similarities

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
