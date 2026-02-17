using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace LearningTrainerShared.Models
{
    public class ChangePasswordRequest
    {
        [Required(ErrorMessage = "Старый пароль обязателен")]
        public string OldPassword { get; set; } = "";

        [Required(ErrorMessage = "Новый пароль обязателен")]
        [MinLength(8, ErrorMessage = "Новый пароль должен быть не менее 8 символов")]
        [MaxLength(128, ErrorMessage = "Пароль не должен превышать 128 символов")]
        [RegularExpression(@"^(?=.*[a-zA-Zа-яА-ЯёЁ])(?=.*[0-9]).+$",
            ErrorMessage = "Пароль должен содержать хотя бы одну букву и одну цифру")]
        public string NewPassword { get; set; } = "";
    }
}
