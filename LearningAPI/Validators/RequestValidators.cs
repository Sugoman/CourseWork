using FluentValidation;
using LearningTrainerShared.Models;
using LearningAPI.Controllers;

namespace LearningAPI.Validators;

public class LoginRequestValidator : AbstractValidator<AuthController.LoginRequest>
{
    public LoginRequestValidator()
    {
        RuleFor(x => x.Username)
            .NotEmpty().WithMessage("Имя пользователя обязательно")
            .MaximumLength(100).WithMessage("Имя пользователя не более 100 символов");

        RuleFor(x => x.Password)
            .NotEmpty().WithMessage("Пароль обязателен");
    }
}

public class RegisterRequestValidator : AbstractValidator<RegisterRequest>
{
    public RegisterRequestValidator()
    {
        RuleFor(x => x.Username)
            .NotEmpty().WithMessage("Имя пользователя обязательно")
            .MinimumLength(3).WithMessage("Имя пользователя должно быть не менее 3 символов")
            .MaximumLength(50).WithMessage("Имя пользователя не более 50 символов")
            .Matches(@"^[a-zA-Z0-9_]+$").WithMessage("Имя пользователя может содержать только буквы, цифры и _");

        RuleFor(x => x.Email)
            .NotEmpty().WithMessage("Email обязателен")
            .EmailAddress().WithMessage("Некорректный формат Email");

        RuleFor(x => x.Password)
            .NotEmpty().WithMessage("Пароль обязателен")
            .MinimumLength(8).WithMessage("Пароль должен быть не менее 8 символов")
            .Matches(@"[a-zA-Z]").WithMessage("Пароль должен содержать хотя бы одну букву")
            .Matches(@"[0-9]").WithMessage("Пароль должен содержать хотя бы одну цифру");
    }
}

public class ChangePasswordRequestValidator : AbstractValidator<ChangePasswordRequest>
{
    public ChangePasswordRequestValidator()
    {
        RuleFor(x => x.OldPassword)
            .NotEmpty().WithMessage("Старый пароль обязателен");

        RuleFor(x => x.NewPassword)
            .NotEmpty().WithMessage("Новый пароль обязателен")
            .MinimumLength(8).WithMessage("Новый пароль должен быть не менее 8 символов")
            .Matches(@"[a-zA-Z]").WithMessage("Пароль должен содержать хотя бы одну букву")
            .Matches(@"[0-9]").WithMessage("Пароль должен содержать хотя бы одну цифру");
    }
}

public class CreateDictionaryRequestValidator : AbstractValidator<CreateDictionaryRequest>
{
    public CreateDictionaryRequestValidator()
    {
        RuleFor(x => x.Name)
            .NotEmpty().WithMessage("Имя словаря обязательно")
            .MaximumLength(100).WithMessage("Имя не более 100 символов");

        RuleFor(x => x.Description)
            .MaximumLength(500).WithMessage("Описание не более 500 символов");

        RuleFor(x => x.LanguageFrom)
            .NotEmpty().WithMessage("Исходный язык обязателен")
            .MaximumLength(50);

        RuleFor(x => x.LanguageTo)
            .NotEmpty().WithMessage("Целевой язык обязателен")
            .MaximumLength(50);
    }
}

public class CreateWordRequestValidator : AbstractValidator<CreateWordRequest>
{
    public CreateWordRequestValidator()
    {
        RuleFor(x => x.OriginalWord)
            .NotEmpty().WithMessage("Слово обязательно")
            .MaximumLength(200).WithMessage("Слово не более 200 символов");

        RuleFor(x => x.Translation)
            .NotEmpty().WithMessage("Перевод обязателен")
            .MaximumLength(500).WithMessage("Перевод не более 500 символов");

        RuleFor(x => x.DictionaryId)
            .GreaterThan(0).WithMessage("ID словаря должен быть указан");
    }
}

public class UpdateProgressRequestValidator : AbstractValidator<UpdateProgressRequest>
{
    public UpdateProgressRequestValidator()
    {
        RuleFor(x => x.WordId)
            .GreaterThan(0).WithMessage("ID слова должен быть указан");

        RuleFor(x => x.Quality)
            .IsInEnum().WithMessage("Некорректное значение качества ответа");
    }
}
