using FluentAssertions;
using FluentValidation.TestHelper;
using StockTracker.Application.Users;
using StockTracker.Domain.Users;

namespace StockTracker.Application.Tests.Users;

public class CreateUserCommandValidatorTests
{
    private readonly CreateUserCommandValidator _validator = new();

    private static CreateUserCommand Valid() =>
        new("Ayşe Yılmaz", "Depo", "ayse@example.com", "Sifre1234", Roles.Staff, CanLogin: true);

    [Fact]
    public void ValidLoginUser_Passes()
    {
        _validator.TestValidate(Valid()).ShouldNotHaveAnyValidationErrors();
    }

    [Fact]
    public void UserWithoutLogin_DoesNotNeedEmailOrPassword()
    {
        var command = Valid() with { Email = null, Password = null, CanLogin = false };

        _validator.TestValidate(command).ShouldNotHaveAnyValidationErrors();
    }

    [Fact]
    public void LoginUser_RequiresEmailAndPassword()
    {
        var result = _validator.TestValidate(Valid() with { Email = null, Password = null });

        result.ShouldHaveValidationErrorFor(x => x.Email);
        result.ShouldHaveValidationErrorFor(x => x.Password);
    }

    [Theory]
    [InlineData("short1A")]
    [InlineData("alllowercase1")]
    [InlineData("ALLUPPERCASE1")]
    [InlineData("NoDigitsHere")]
    public void WeakPassword_Fails(string password)
    {
        _validator.TestValidate(Valid() with { Password = password }).ShouldHaveValidationErrorFor(x => x.Password);
    }

    [Fact]
    public void FieldLengthsAndRole_AreValidated()
    {
        var command = Valid() with
        {
            FullName = new string('a', 101),
            Department = new string('b', 101),
            Email = "not-an-email",
            Role = "SuperUser",
        };

        var result = _validator.TestValidate(command);

        result.ShouldHaveValidationErrorFor(x => x.FullName);
        result.ShouldHaveValidationErrorFor(x => x.Department);
        result.ShouldHaveValidationErrorFor(x => x.Email);
        result.ShouldHaveValidationErrorFor(x => x.Role);
    }
}

public class UpdateUserCommandValidatorTests
{
    private readonly UpdateUserCommandValidator _validator = new();

    [Fact]
    public void PasswordIsOptional()
    {
        var command = new UpdateUserCommand(Guid.NewGuid(), "Ali", null, "ali@example.com", null, Roles.Admin, true);

        _validator.TestValidate(command).ShouldNotHaveAnyValidationErrors();
    }

    [Fact]
    public void ProvidedPassword_MustMeetPolicy()
    {
        var command = new UpdateUserCommand(Guid.NewGuid(), "Ali", null, "ali@example.com", "weak", Roles.Admin, true);

        _validator.TestValidate(command).ShouldHaveValidationErrorFor(x => x.Password);
    }
}

public class GetUsersQueryValidatorTests
{
    private readonly GetUsersQueryValidator _validator = new();

    [Theory]
    [InlineData(0, 20, null)]
    [InlineData(1, 101, null)]
    [InlineData(1, 20, "password")]
    public void InvalidPagingOrSort_Fails(int page, int pageSize, string? sort)
    {
        _validator.TestValidate(new GetUsersQuery(page, pageSize, null, sort)).IsValid.Should().BeFalse();
    }

    [Theory]
    [InlineData("fullName")]
    [InlineData("-role")]
    public void KnownSortFields_Pass(string sort)
    {
        _validator.TestValidate(new GetUsersQuery(1, 100, "ali", sort)).IsValid.Should().BeTrue();
    }
}
