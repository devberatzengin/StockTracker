using FluentAssertions;
using FluentValidation.TestHelper;
using StockTracker.Application.Inventory;
using StockTracker.Domain.Inventory;

namespace StockTracker.Application.Tests.Inventory;

public class MovementCommandValidatorTests
{
    private static readonly Guid Product = Guid.NewGuid();
    private static readonly Guid Location = Guid.NewGuid();

    [Fact]
    public void StockIn_Valid_Passes()
    {
        var command = new StockInCommand(Location, [new StockInLine(Product, 500, null), new StockInLine(Product, 3, ["SN-1"])]);

        new StockInCommandValidator().TestValidate(command).ShouldNotHaveAnyValidationErrors();
    }

    [Fact]
    public void StockIn_RejectsMissingTargetEmptyLinesAndMoreThan100Lines()
    {
        var validator = new StockInCommandValidator();

        validator.TestValidate(new StockInCommand(Guid.Empty, [])).IsValid.Should().BeFalse();
        var tooMany = Enumerable.Range(0, 101).Select(_ => new StockInLine(Product, 1, null)).ToList();
        validator.TestValidate(new StockInCommand(Location, tooMany)).ShouldHaveValidationErrorFor(x => x.Lines);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-5)]
    [InlineData(1.2345)]
    public void StockIn_RejectsInvalidQuantity(decimal quantity)
    {
        var result = new StockInCommandValidator().TestValidate(new StockInCommand(Location, [new StockInLine(Product, quantity, null)]));

        result.ShouldHaveValidationErrorFor("Lines[0].Quantity");
    }

    [Fact]
    public void StockIn_RejectsTooLongSerialNumber()
    {
        var command = new StockInCommand(Location, [new StockInLine(Product, 1, [new string('S', 101)])]);

        new StockInCommandValidator().TestValidate(command).ShouldHaveValidationErrorFor("Lines[0].SerialNumbers[0]");
    }

    [Fact]
    public void StockOut_ReasonOtherRequiresNote()
    {
        var line = new StockLine(null, Product, Location, 1);
        var validator = new StockOutCommandValidator();

        validator.TestValidate(new StockOutCommand(MovementReason.Other, null, [line])).ShouldHaveValidationErrorFor(x => x.Note);
        validator.TestValidate(new StockOutCommand(MovementReason.Other, "sayım farkı", [line])).ShouldNotHaveAnyValidationErrors();
        validator.TestValidate(new StockOutCommand(MovementReason.Consumed, null, [line])).ShouldNotHaveAnyValidationErrors();
    }

    [Theory]
    [InlineData(true, true, false, false)]
    [InlineData(false, true, false, true)]
    [InlineData(false, false, true, true)]
    [InlineData(false, false, false, false)]
    public void Lines_AreEitherAnItemOrAProductAtALocationWithQuantity(bool item, bool product, bool location, bool quantity)
    {
        var line = new StockLine(
            item ? Guid.NewGuid() : null,
            product ? Product : null,
            location ? Location : null,
            quantity ? 1 : null);

        new StockTransferCommandValidator().TestValidate(new StockTransferCommand(Location, [line])).IsValid.Should().BeFalse();
    }

    [Fact]
    public void Transfer_ValidLines_Pass()
    {
        var command = new StockTransferCommand(Location, [new StockLine(Guid.NewGuid(), null, null, null), new StockLine(null, Product, Location, 2.5m)]);

        new StockTransferCommandValidator().TestValidate(command).ShouldNotHaveAnyValidationErrors();
    }
}
