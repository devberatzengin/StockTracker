using FluentAssertions;
using FluentValidation;
using MediatR;
using StockTracker.Application.Common.Behaviors;
using ValidationException = StockTracker.Application.Common.Exceptions.ValidationException;

namespace StockTracker.Application.Tests.Common.Behaviors;

public class ValidationBehaviorTests
{
    public sealed record TestRequest(string Name, int Quantity) : IRequest<string>;

    private sealed class NameValidator : AbstractValidator<TestRequest>
    {
        public NameValidator()
        {
            RuleFor(r => r.Name).NotEmpty().WithMessage("Name is required.");
        }
    }

    private sealed class QuantityValidator : AbstractValidator<TestRequest>
    {
        public QuantityValidator()
        {
            RuleFor(r => r.Quantity).GreaterThan(0).WithMessage("Quantity must be positive.");
            RuleFor(r => r.Quantity).LessThan(100).WithMessage("Quantity must be less than 100.");
        }
    }

    private static readonly IValidator<TestRequest>[] Validators = [new NameValidator(), new QuantityValidator()];

    private static (RequestHandlerDelegate<string> Next, Func<bool> WasCalled) CreateNext()
    {
        var called = false;
        RequestHandlerDelegate<string> next = _ =>
        {
            called = true;
            return Task.FromResult("handled");
        };
        return (next, () => called);
    }

    [Fact]
    public async Task Handle_WithValidRequest_CallsNext()
    {
        var behavior = new ValidationBehavior<TestRequest, string>(Validators);
        var (next, wasCalled) = CreateNext();

        var result = await behavior.Handle(new TestRequest("Laptop", 5), next, CancellationToken.None);

        result.Should().Be("handled");
        wasCalled().Should().BeTrue();
    }

    [Fact]
    public async Task Handle_WithoutValidators_CallsNext()
    {
        var behavior = new ValidationBehavior<TestRequest, string>([]);
        var (next, wasCalled) = CreateNext();

        await behavior.Handle(new TestRequest("", -1), next, CancellationToken.None);

        wasCalled().Should().BeTrue();
    }

    [Fact]
    public async Task Handle_WithInvalidRequest_ThrowsWithErrorsFromAllValidatorsAndSkipsNext()
    {
        var behavior = new ValidationBehavior<TestRequest, string>(Validators);
        var (next, wasCalled) = CreateNext();

        var act = () => behavior.Handle(new TestRequest("", 0), next, CancellationToken.None);

        var exception = (await act.Should().ThrowAsync<ValidationException>()).Which;
        exception.Errors.Should().BeEquivalentTo(new Dictionary<string, string[]>
        {
            ["Name"] = ["Name is required."],
            ["Quantity"] = ["Quantity must be positive."],
        });
        wasCalled().Should().BeFalse();
    }

    [Fact]
    public async Task Handle_WithMultipleFailuresOnSameProperty_GroupsThem()
    {
        var validator = new InlineValidator<TestRequest>();
        validator.RuleFor(r => r.Name).NotEmpty().WithMessage("Name is required.");
        validator.RuleFor(r => r.Name).MinimumLength(3).WithMessage("Name is too short.");
        var behavior = new ValidationBehavior<TestRequest, string>([validator]);
        var (next, _) = CreateNext();

        var act = () => behavior.Handle(new TestRequest("", 1), next, CancellationToken.None);

        var exception = (await act.Should().ThrowAsync<ValidationException>()).Which;
        exception.Errors["Name"].Should().BeEquivalentTo("Name is required.", "Name is too short.");
    }
}
