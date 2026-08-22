using FluentValidation;
using Learnier.Application.Common.Abstractions;
using Learnier.Application.Common.Results;

namespace Learnier.Application.Features.Accounts.Commands.ChangeMyPassword;

public sealed record ChangeMyPasswordCommand(
    string CurrentPassword,
    string NewPassword);

internal sealed class ChangeMyPasswordValidator : AbstractValidator<ChangeMyPasswordCommand>
{
    public ChangeMyPasswordValidator()
    {
        RuleFor(command => command.CurrentPassword)
            .NotEmpty().WithErrorCode("account.current_password_required");

        RuleFor(command => command.NewPassword)
            .NotEmpty().WithErrorCode("auth.password_required")
            .MinimumLength(8).WithErrorCode("auth.password_too_short")
            .MaximumLength(128).WithErrorCode("auth.password_too_long");
    }
}

public sealed class ChangeMyPasswordHandler(
    IUserRepository users,
    IPasswordHasher passwordHasher,
    ICurrentUser currentUser,
    IUnitOfWork unitOfWork)
{
    public async Task<Result> Handle(
        ChangeMyPasswordCommand command,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        if (currentUser.UserId is not { } userId)
        {
            return Result.Failure(Error.Unauthorized("common.unauthorized"));
        }

        var user = await users.FindByIdAsync(userId, cancellationToken);
        if (user?.PasswordHash is null)
        {
            return Result.Failure(Error.Unauthorized("common.unauthorized"));
        }

        var verification = passwordHasher.Verify(user.PasswordHash, command.CurrentPassword);
        if (verification is PasswordVerificationOutcome.Failed)
        {
            return Result.Failure(Error.Validation("account.current_password_invalid"));
        }

        user.ChangePasswordHash(passwordHasher.Hash(command.NewPassword));
        await unitOfWork.SaveChangesAsync(cancellationToken);

        return Result.Success();
    }
}
