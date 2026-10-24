using Microsoft.AspNetCore.Identity;

namespace StockTracker.Infrastructure.Identity;

/// <summary>
/// Turkish messages for Identity errors that can reach the user. Validators catch most cases first;
/// these cover what only Identity can detect.
/// </summary>
public sealed class TurkishIdentityErrorDescriber : IdentityErrorDescriber
{
    public override IdentityError DefaultError() =>
        Error(nameof(DefaultError), "Beklenmeyen bir hata oluştu.");

    public override IdentityError ConcurrencyFailure() =>
        Error(nameof(ConcurrencyFailure), "Kayıt başka bir işlem tarafından değiştirildi.");

    public override IdentityError PasswordMismatch() =>
        Error(nameof(PasswordMismatch), "Şifre hatalı.");

    public override IdentityError InvalidUserName(string? userName) =>
        Error(nameof(InvalidUserName), "Kullanıcı adı geçersiz karakterler içeriyor.");

    public override IdentityError InvalidEmail(string? email) =>
        Error(nameof(InvalidEmail), "Geçerli bir e-posta adresi girin.");

    public override IdentityError DuplicateUserName(string userName) =>
        Error(nameof(DuplicateUserName), "Bu e-posta adresi zaten kullanılıyor.");

    public override IdentityError DuplicateEmail(string email) =>
        Error(nameof(DuplicateEmail), "Bu e-posta adresi zaten kullanılıyor.");

    public override IdentityError UserAlreadyHasPassword() =>
        Error(nameof(UserAlreadyHasPassword), "Kullanıcının zaten bir şifresi var.");

    public override IdentityError UserAlreadyInRole(string role) =>
        Error(nameof(UserAlreadyInRole), "Kullanıcı bu role zaten sahip.");

    public override IdentityError UserNotInRole(string role) =>
        Error(nameof(UserNotInRole), "Kullanıcı bu role sahip değil.");

    public override IdentityError PasswordTooShort(int length) =>
        Error(nameof(PasswordTooShort), $"Şifre en az {length} karakter olmalıdır.");

    public override IdentityError PasswordRequiresUniqueChars(int uniqueChars) =>
        Error(nameof(PasswordRequiresUniqueChars), $"Şifre en az {uniqueChars} farklı karakter içermelidir.");

    public override IdentityError PasswordRequiresNonAlphanumeric() =>
        Error(nameof(PasswordRequiresNonAlphanumeric), "Şifre en az bir özel karakter içermelidir.");

    public override IdentityError PasswordRequiresDigit() =>
        Error(nameof(PasswordRequiresDigit), "Şifre en az bir rakam içermelidir.");

    public override IdentityError PasswordRequiresLower() =>
        Error(nameof(PasswordRequiresLower), "Şifre en az bir küçük harf içermelidir.");

    public override IdentityError PasswordRequiresUpper() =>
        Error(nameof(PasswordRequiresUpper), "Şifre en az bir büyük harf içermelidir.");

    private static IdentityError Error(string code, string description) => new() { Code = code, Description = description };
}
