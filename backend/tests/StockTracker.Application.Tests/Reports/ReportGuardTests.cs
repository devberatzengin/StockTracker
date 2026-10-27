using FluentAssertions;
using StockTracker.Application.Reports;
using StockTracker.Domain.Exceptions;

namespace StockTracker.Application.Tests.Reports;

public class ReportGuardTests
{
    [Fact]
    public void RowLimit_AllowsExactly50000()
    {
        var act = () => ReportGuard.EnsureRowLimit(ReportLimits.MaxRows, "Tarih aralığını daraltın.");

        act.Should().NotThrow();
    }

    [Fact]
    public void RowLimit_RejectsMore_WithAHint()
    {
        var act = () => ReportGuard.EnsureRowLimit(ReportLimits.MaxRows + 1, "Tarih aralığını daraltın.");

        var error = act.Should().Throw<DomainException>().Which;
        error.Code.Should().Be(ReportLimits.TooManyRows);
        error.Message.Should().Contain("50.001").And.Contain("Tarih aralığını daraltın.");
    }
}
