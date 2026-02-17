using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace LearningTrainerShared.Models
{
    public class RegisterRequest
    {
        [Required(ErrorMessage = "Имя пользователя обязательно")]
        [StringLength(50, MinimumLength = 3, ErrorMessage = "Имя пользователя должно быть от 3 до 50 символов")]
        [RegularExpression(@"^[a-zA-Z0-9_]+$", ErrorMessage = "Имя пользователя может содержать только буквы, цифры и _")]
        public string Username { get; set; } = "";

        [Required(ErrorMessage = "Email обязателен")]
        [EmailAddress(ErrorMessage = "Некорректный формат Email")]
        public string Email { get; set; } = "";

        [Required(ErrorMessage = "Пароль обязателен")]
        [MinLength(8, ErrorMessage = "Пароль должен быть не менее 8 символов")]
        [MaxLength(128, ErrorMessage = "Пароль не должен превышать 128 символов")]
        [RegularExpression(@"^(?=.*[a-zA-Zа-яА-ЯёЁ])(?=.*[0-9]).+$",
            ErrorMessage = "Пароль должен содержать хотя бы одну букву и одну цифру")]
        public string Password { get; set; } = "";

        public string? InviteCode { get; set; }

        // Для обратной совместимости
        [Obsolete("Use Username instead")]
        public string? Login { get => Username; set => Username = value ?? ""; }
    }
}
