using System.Text.RegularExpressions;

namespace LearningTrainerShared.Services;

/// <summary>
/// Единый валидатор пароля — используется во всех проектах (API, WPF, Blazor).
/// Правила: ≥ 8 символов, хотя бы 1 буква, хотя бы 1 цифра.
/// </summary>
public static class PasswordValidator
{
    public const int MinLength = 8;
    public const int MaxLength = 128;

    /// <summary>
    /// Проверяет пароль на соответствие всем правилам.
    /// Возвращает список ошибок (пустой — если пароль валиден).
    /// </summary>
    public static List<string> Validate(string? password)
    {
        var errors = new List<string>();

        if (string.IsNullOrWhiteSpace(password))
        {
            errors.Add("Пароль обязателен");
            return errors;
        }

        if (password.Length < MinLength)
            errors.Add($"Пароль должен быть не менее {MinLength} символов");

        if (password.Length > MaxLength)
            errors.Add($"Пароль не должен превышать {MaxLength} символов");

        if (!Regex.IsMatch(password, @"[a-zA-Zа-яА-ЯёЁ]"))
            errors.Add("Пароль должен содержать хотя бы одну букву");

        if (!Regex.IsMatch(password, @"[0-9]"))
            errors.Add("Пароль должен содержать хотя бы одну цифру");

        return errors;
    }

    /// <summary>
    /// Быстрая проверка — валиден ли пароль.
    /// </summary>
    public static bool IsValid(string? password) => Validate(password).Count == 0;

    /// <summary>
    /// Вычисляет уровень надёжности пароля (0–4).
    /// 0 = очень слабый, 1 = слабый, 2 = средний, 3 = хороший, 4 = отличный.
    /// </summary>
    public static PasswordStrength GetStrength(string? password)
    {
        if (string.IsNullOrEmpty(password))
            return PasswordStrength.VeryWeak;

        int score = 0;

        // Длина
        if (password.Length >= MinLength) score++;
        if (password.Length >= 12) score++;

        // Цифры
        if (Regex.IsMatch(password, @"[0-9]")) score++;

        // Буквы в разном регистре
        if (Regex.IsMatch(password, @"[a-zа-яё]") && Regex.IsMatch(password, @"[A-ZА-ЯЁ]")) score++;

        // Спецсимволы
        if (Regex.IsMatch(password, @"[^a-zA-Zа-яА-ЯёЁ0-9\s]")) score++;

        return score switch
        {
            0 => PasswordStrength.VeryWeak,
            1 => PasswordStrength.Weak,
            2 => PasswordStrength.Fair,
            3 => PasswordStrength.Good,
            _ => PasswordStrength.Strong
        };
    }
}

/// <summary>
/// Уровни надёжности пароля.
/// </summary>
public enum PasswordStrength
{
    VeryWeak = 0,
    Weak = 1,
    Fair = 2,
    Good = 3,
    Strong = 4
}
