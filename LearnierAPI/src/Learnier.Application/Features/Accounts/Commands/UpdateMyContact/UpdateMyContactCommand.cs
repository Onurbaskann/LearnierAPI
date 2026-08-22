using FluentValidation;
using Learnier.Application.Common.Abstractions;
using Learnier.Application.Common.Results;
using Learnier.Application.Features.Authentication;

namespace Learnier.Application.Features.Accounts.Commands.UpdateMyContact;

public sealed record UpdateMyContactCommand(
    string Email,
    string FirstName,
    string LastName,
    string? Phone);

internal sealed class UpdateMyContactValidator : AbstractValidator<UpdateMyContactCommand>
{
    public UpdateMyContactValidator()
    {
        RuleFor(c => c.Email)
            .NotEmpty().WithErrorCode("account.email_required")
            .EmailAddress().WithErrorCode("account.email_invalid")
            .MaximumLength(320).WithErrorCode("account.email_too_long");

        RuleFor(c => c.FirstName)
            .NotEmpty().WithErrorCode("account.first_name_required")
            .MaximumLength(100).WithErrorCode("account.first_name_too_long");

        RuleFor(c => c.LastName)
            .NotEmpty().WithErrorCode("account.last_name_required")
            .MaximumLength(100).WithErrorCode("account.last_name_too_long");

        RuleFor(c => c.Phone)
            .MaximumLength(32).WithErrorCode("account.phone_too_long");
    }
}

public sealed class UpdateMyContactHandler(
    IUserRepository users,
    ICurrentUser currentUser,
    IUnitOfWork unitOfWork)
{
    public async Task<Result<AccountContact>> Handle(
        UpdateMyContactCommand command,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        if (currentUser.UserId is not { } userId)
        {
            return Error.Unauthorized("common.unauthorized");
        }

        var user = await users.FindByIdAsync(userId, cancellationToken);
        if (user is null)
        {
            return Error.Unauthorized("common.unauthorized");
        }

        var emailOwner = await users.FindByEmailAsync(command.Email, cancellationToken);
        if (emailOwner is not null && emailOwner.Id != userId)
        {
            return AuthenticationErrors.EmailAlreadyRegistered;
        }

        user.UpdateContact(command.Email, command.FirstName, command.LastName, command.Phone);
        await unitOfWork.SaveChangesAsync(cancellationToken);

        return new AccountContact(
            user.Id, user.Email, user.FirstName, user.LastName, user.Phone);
    }
}
