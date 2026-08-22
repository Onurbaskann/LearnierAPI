namespace Learnier.Application.Features.Accounts;

public sealed record AccountContact(
    Guid UserId,
    string Email,
    string FirstName,
    string LastName,
    string? Phone);
