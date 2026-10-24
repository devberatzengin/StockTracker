using FluentValidation;

namespace StockTracker.Application.Common.Validation;

public static class ValidationRules
{
    public const int EmailMaxLength = 256;

    /// <summary>Mirrors the Identity password options: 8+ chars with upper, lower and digit.</summary>
    public static IRuleBuilderOptions<T, string?> Password<T>(this IRuleBuilder<T, string?> rule) =>
        rule
            .MinimumLength(8).WithMessage("Şifre en az 8 karakter olmalıdır.")
            .Matches("[A-Z]").WithMessage("Şifre en az bir büyük harf içermelidir.")
            .Matches("[a-z]").WithMessage("Şifre en az bir küçük harf içermelidir.")
            .Matches("[0-9]").WithMessage("Şifre en az bir rakam içermelidir.");

    public static IRuleBuilderOptions<T, string?> Email<T>(this IRuleBuilder<T, string?> rule) =>
        rule
            .EmailAddress().WithMessage("Geçerli bir e-posta adresi girin.")
            .MaximumLength(EmailMaxLength).WithMessage($"E-posta en fazla {EmailMaxLength} karakter olabilir.");

}
