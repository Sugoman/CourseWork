using FluentAssertions;
using LearningTrainerShared.Services;
using Xunit;

namespace LearningAPI.Tests.Services;

public class PasswordValidatorTests
{
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Validate_NullOrWhitespace_ReturnsError(string? password)
    {
        var errors = PasswordValidator.Validate(password);
        errors.Should().Contain(e => e.Contains("обязателен"));
    }

    [Theory]
    [InlineData("a1")]
    [InlineData("abc12")]
    [InlineData("Pass1")]
    [InlineData("Ab1234")]
    [InlineData("1234567")]
    public void Validate_TooShort_ReturnsMinLengthError(string password)
    {
        var errors = PasswordValidator.Validate(password);
        errors.Should().Contain(e => e.Contains("не менее"));
    }

    [Theory]
    [InlineData("12345678")]
    [InlineData("123456789")]
    public void Validate_NoLetters_ReturnsLetterError(string password)
    {
        var errors = PasswordValidator.Validate(password);
        errors.Should().Contain(e => e.Contains("букву"));
    }

    [Theory]
    [InlineData("abcdefgh")]
    [InlineData("Password")]
    public void Validate_NoDigits_ReturnsDigitError(string password)
    {
        var errors = PasswordValidator.Validate(password);
        errors.Should().Contain(e => e.Contains("цифру"));
    }

    [Theory]
    [InlineData("Password1")]
    [InlineData("mypass12")]
    [InlineData("Str0ngP@ss")]
    [InlineData("12345abc")]
    [InlineData("пароль12")]
    public void Validate_ValidPassword_ReturnsNoErrors(string password)
    {
        var errors = PasswordValidator.Validate(password);
        errors.Should().BeEmpty();
    }

    [Theory]
    [InlineData("Password1")]
    [InlineData("mypass12")]
    [InlineData("Str0ngP@ss")]
    public void IsValid_ValidPassword_ReturnsTrue(string password)
    {
        PasswordValidator.IsValid(password).Should().BeTrue();
    }

    [Theory]
    [InlineData("short1")]
    [InlineData("")]
    [InlineData("nope")]
    public void IsValid_InvalidPassword_ReturnsFalse(string password)
    {
        PasswordValidator.IsValid(password).Should().BeFalse();
    }

    [Fact]
    public void GetStrength_NullOrEmpty_ReturnsVeryWeak()
    {
        PasswordValidator.GetStrength(null).Should().Be(PasswordStrength.VeryWeak);
        PasswordValidator.GetStrength("").Should().Be(PasswordStrength.VeryWeak);
    }

    [Fact]
    public void GetStrength_ShortNoVariety_ReturnsWeak()
    {
        var strength = PasswordValidator.GetStrength("abc");
        ((int)strength).Should().BeLessThan((int)PasswordStrength.Fair);
    }

    [Fact]
    public void GetStrength_LongMixedCaseDigitsSpecial_ReturnsStrongOrGood()
    {
        var strength = PasswordValidator.GetStrength("MyStr0ng!Pass");
        strength.Should().BeOneOf(PasswordStrength.Good, PasswordStrength.Strong);
    }

    [Fact]
    public void GetStrength_CyrillicPassword_CountsLetters()
    {
        // Кириллица должна считаться как буква
        var strength = PasswordValidator.GetStrength("Пароль123!");
        ((int)strength).Should().BeGreaterThanOrEqualTo((int)PasswordStrength.Good);
    }
}
