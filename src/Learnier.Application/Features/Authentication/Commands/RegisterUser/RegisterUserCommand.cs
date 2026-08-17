namespace Learnier.Application.Features.Authentication.Commands.RegisterUser;

public sealed record RegisterUserCommand(
    string Email,
    string Password,
    string FirstName,
    string LastName);

public sealed record RegisterUserResult(Guid UserId, string Email);
