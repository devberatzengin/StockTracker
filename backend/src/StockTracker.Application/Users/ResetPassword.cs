using FluentValidation;
using MediatR;
using StockTracker.Application.Common.Interfaces;
using StockTracker.Application.Common.Validation;

namespace StockTracker.Application.Users;

public sealed record ResetPasswordCommand(Guid Id, string NewPassword) : IRequest;

public sealed class ResetPasswordCommandValidator : AbstractValidator<ResetPasswordCommand>
{
    public ResetPasswordCommandValidator()
    {
        RuleFor(x => x.NewPassword).NotEmpty().WithMessage("Yeni şifre zorunludur.").Password();
    }
}

public sealed class ResetPasswordCommandHandler(IUserAccountService users) : IRequestHandler<ResetPasswordCommand>
{
    public Task Handle(ResetPasswordCommand request, CancellationToken cancellationToken) =>
        users.ResetPasswordAsync(request.Id, request.NewPassword, cancellationToken);
}
