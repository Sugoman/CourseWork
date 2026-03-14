namespace LearningTrainerShared.Services;

/// <summary>
/// Категория ошибки при вводе (§1.4 LEARNING_IMPROVEMENTS).
/// </summary>
public enum TypingErrorType
{
    None,
    Typo,
    PartialRecall,
    FullMiss,
    Confusion
}

/// <summary>
/// Статус символа/фрагмента в посимвольном diff.
/// </summary>
public enum DiffStatus
{
    Match,
    Wrong,
    Missing,
    Extra
}

/// <summary>
/// Один фрагмент посимвольного diff.
/// </summary>
public sealed class DiffSegment
{
    public string Text { get; init; } = "";
    public DiffStatus Status { get; init; }
}

/// <summary>
/// Результат анализа Typing-ответа.
/// </summary>
public sealed class TypingFeedbackResult
{
    public TypingErrorType ErrorType { get; init; }
    public List<DiffSegment> UserSegments { get; init; } = new();
    public List<DiffSegment> CorrectSegments { get; init; } = new();
    public double Similarity { get; init; }
    public int LevenshteinDistance { get; init; }
}

/// <summary>
/// Посимвольный анализ ответа для режимов Typing / Listening (§1.4 LEARNING_IMPROVEMENTS).
/// Содержит Levenshtein, LCS diff и классификацию ошибки.
/// </summary>
public static class TypingFeedbackService
{
    /// <summary>
    /// Анализирует ответ пользователя и возвращает посимвольный diff + категорию ошибки.
    /// </summary>
    public static TypingFeedbackResult Analyze(string userAnswer, string correctAnswer)
    {
        var user = (userAnswer ?? "").Trim();
        var correct = (correctAnswer ?? "").Trim();
        var userLower = user.ToLowerInvariant();
        var correctLower = correct.ToLowerInvariant();

        if (userLower == correctLower)
        {
            return new TypingFeedbackResult
            {
                ErrorType = TypingErrorType.None,
                UserSegments = new List<DiffSegment> { new() { Text = user, Status = DiffStatus.Match } },
                CorrectSegments = new List<DiffSegment> { new() { Text = correct, Status = DiffStatus.Match } },
                Similarity = 1.0,
                LevenshteinDistance = 0
            };
        }

        int levDist = ComputeLevenshtein(userLower, correctLower);
        var lcs = ComputeLcs(userLower, correctLower);

        int maxLen = Math.Max(userLower.Length, correctLower.Length);
        double similarity = maxLen > 0 ? (double)lcs.Count / maxLen : 0;

        // Build per-character segments
        var userSegments = BuildUserSegments(user, correct, lcs);
        var correctSegments = BuildCorrectSegments(user, correct, lcs);

        // Classify error
        var errorType = ClassifyError(levDist, similarity, correctLower.Length);

        return new TypingFeedbackResult
        {
            ErrorType = errorType,
            UserSegments = userSegments,
            CorrectSegments = correctSegments,
            Similarity = Math.Round(similarity, 3),
            LevenshteinDistance = levDist
        };
    }

    /// <summary>
    /// Возвращает true, если ошибка — опечатка и не должна сбрасывать SM-2.
    /// </summary>
    public static bool IsTypo(TypingFeedbackResult result)
        => result.ErrorType == TypingErrorType.Typo;

    private static TypingErrorType ClassifyError(int levDist, double similarity, int correctLength)
    {
        // Typo: Levenshtein ≤ 2 AND word length > 5 (spec §1.4)
        if (levDist <= 2 && correctLength > 5)
            return TypingErrorType.Typo;

        // Typo: single char error on shorter words (≤5) only if distance = 1
        if (levDist == 1 && correctLength >= 3)
            return TypingErrorType.Typo;

        // Partial recall: >50% matched via LCS
        if (similarity > 0.5)
            return TypingErrorType.PartialRecall;

        return TypingErrorType.FullMiss;
    }

    private static List<DiffSegment> BuildUserSegments(string user, string correct, List<(int, int)> lcs)
    {
        var segments = new List<DiffSegment>();
        var lcsUserIndices = new HashSet<int>(lcs.Select(p => p.Item1));

        int li = 0;
        int ui = 0;

        while (ui < user.Length)
        {
            if (li < lcs.Count && ui == lcs[li].Item1)
            {
                // Matched char
                AppendToSegments(segments, user[ui].ToString(), DiffStatus.Match);
                li++;
                ui++;
            }
            else if (li < lcs.Count && ui < lcs[li].Item1)
            {
                // Extra char in user input (not in correct)
                AppendToSegments(segments, user[ui].ToString(), DiffStatus.Extra);
                ui++;
            }
            else
            {
                // Beyond LCS — extra chars
                AppendToSegments(segments, user[ui].ToString(), DiffStatus.Extra);
                ui++;
            }
        }

        return segments;
    }

    private static List<DiffSegment> BuildCorrectSegments(string user, string correct, List<(int, int)> lcs)
    {
        var segments = new List<DiffSegment>();
        int li = 0;
        int ci = 0;

        while (ci < correct.Length)
        {
            if (li < lcs.Count && ci == lcs[li].Item2)
            {
                // Matched char
                AppendToSegments(segments, correct[ci].ToString(), DiffStatus.Match);
                li++;
                ci++;
            }
            else if (li < lcs.Count && ci < lcs[li].Item2)
            {
                // Missing char (not typed by user)
                AppendToSegments(segments, correct[ci].ToString(), DiffStatus.Missing);
                ci++;
            }
            else
            {
                // Beyond LCS — missing chars
                AppendToSegments(segments, correct[ci].ToString(), DiffStatus.Missing);
                ci++;
            }
        }

        return segments;
    }

    private static void AppendToSegments(List<DiffSegment> segments, string ch, DiffStatus status)
    {
        if (segments.Count > 0 && segments[^1].Status == status)
        {
            // Merge consecutive segments with same status
            var last = segments[^1];
            segments[^1] = new DiffSegment { Text = last.Text + ch, Status = status };
        }
        else
        {
            segments.Add(new DiffSegment { Text = ch, Status = status });
        }
    }

    public static int ComputeLevenshtein(string s, string t)
    {
        if (string.IsNullOrEmpty(s)) return t?.Length ?? 0;
        if (string.IsNullOrEmpty(t)) return s.Length;

        var d = new int[s.Length + 1, t.Length + 1];
        for (int i = 0; i <= s.Length; i++) d[i, 0] = i;
        for (int j = 0; j <= t.Length; j++) d[0, j] = j;

        for (int i = 1; i <= s.Length; i++)
        {
            for (int j = 1; j <= t.Length; j++)
            {
                int cost = s[i - 1] == t[j - 1] ? 0 : 1;
                d[i, j] = Math.Min(
                    Math.Min(d[i - 1, j] + 1, d[i, j - 1] + 1),
                    d[i - 1, j - 1] + cost);
            }
        }
        return d[s.Length, t.Length];
    }

    public static List<(int, int)> ComputeLcs(string a, string b)
    {
        int m = a.Length, n = b.Length;
        var dp = new int[m + 1, n + 1];

        for (int i = 1; i <= m; i++)
            for (int j = 1; j <= n; j++)
                dp[i, j] = a[i - 1] == b[j - 1]
                    ? dp[i - 1, j - 1] + 1
                    : Math.Max(dp[i - 1, j], dp[i, j - 1]);

        var result = new List<(int, int)>();
        int x = m, y = n;
        while (x > 0 && y > 0)
        {
            if (a[x - 1] == b[y - 1])
            {
                result.Add((x - 1, y - 1));
                x--; y--;
            }
            else if (dp[x - 1, y] >= dp[x, y - 1])
                x--;
            else
                y--;
        }

        result.Reverse();
        return result;
    }

    /// <summary>
    /// Возвращает локализованное название категории ошибки.
    /// </summary>
    public static string GetErrorLabel(TypingErrorType errorType) => errorType switch
    {
        TypingErrorType.Typo => "Опечатка",
        TypingErrorType.PartialRecall => "Частичное запоминание",
        TypingErrorType.FullMiss => "Не вспомнил",
        TypingErrorType.Confusion => "Путаница",
        _ => ""
    };
}
