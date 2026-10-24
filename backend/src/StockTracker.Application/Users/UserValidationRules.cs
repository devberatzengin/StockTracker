using FluentValidation;
using StockTracker.Application.Common.Validation;
using StockTracker.Domain.Users;

namespace StockTracker.Application.Users;

/// <summary>Fields shared by user create and update commands.</summary>
public interface IUserFields
{
    string FullName { get; }

    string? Department { get; }

    string? Email { get; }

    string Role { get; }

    bool CanLogin { get; }
}

public sealed class UserFieldsValidator : AbstractValidator<IUserFields>
{
    public const int FullNameMaxLength = 100;
    public const int DepartmentMaxLength = 100;

    public UserFieldsValidator()
    {
        RuleFor(x => x.FullName)
            .NotEmpty().WithMessage("Ad soyad zorunludur.")
            .MaximumLength(FullNameMaxLength).WithMessage($"Ad soyad en fazla {FullNameMaxLength} karakter olabilir.");

        RuleFor(x => x.Department)
            .MaximumLength(DepartmentMaxLength).WithMessage($"Departman en fazla {DepartmentMaxLength} karakter olabilir.");

        RuleFor(x => x.Role)
            .Must(Roles.IsValid).WithMessage($"Rol şunlardan biri olmalıdır: {string.Join(", ", Roles.All)}.");

        RuleFor(x => x.Email)
            .NotEmpty().When(x => x.CanLogin).WithMessage("Giriş yetkisi olan kullanıcı için e-posta zorunludur.");
        RuleFor(x => x.Email).Email().When(x => !string.IsNullOrEmpty(x.Email));
    }
}
