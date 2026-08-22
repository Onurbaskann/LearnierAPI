using Learnier.Application.Common.Results;

namespace Learnier.Application.Features.Messages;

internal static class MessageErrors
{
    public static Error UserNotFound => Error.NotFound("messages.user_not_found");

    public static Error CannotMessageSelf => Error.Validation("messages.cannot_message_self");

    /// <summary>
    /// Yazisma kanali kapali: taraflar ne arkadas ne de ortak bir dersi var.
    /// Bkz. <see cref="MessagingAccess"/>.
    /// </summary>
    public static Error NotReachable => Error.Forbidden("messages.not_reachable");
}
