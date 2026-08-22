using FluentValidation;
using Learnier.Application.Common.Abstractions;
using Learnier.Application.Common.Results;
using Learnier.Domain.Social;

namespace Learnier.Application.Features.Messages;

public sealed record ConversationListItem(
    Guid PeerUserId,
    string Email,
    string FirstName,
    string LastName,
    string LastMessageBody,
    DateTimeOffset LastMessageAt,
    bool LastMessageFromMe,
    int UnreadCount);

public sealed record MessageListItem(
    Guid MessageId,
    Guid SenderUserId,
    bool IsMine,
    string Body,
    DateTimeOffset SentAt,
    DateTimeOffset? ReadAt);

public sealed record MessageThread(
    Guid PeerUserId,
    string FirstName,
    string LastName,
    IReadOnlyList<MessageListItem> Messages);

public sealed record UnreadMessageCount(int Count);

public sealed record SendMessageCommand(Guid RecipientUserId, string Body);

internal sealed class SendMessageValidator : AbstractValidator<SendMessageCommand>
{
    public SendMessageValidator()
    {
        RuleFor(command => command.RecipientUserId)
            .NotEmpty().WithErrorCode("messages.recipient_required");

        RuleFor(command => command.Body)
            .NotEmpty().WithErrorCode("messages.body_required")
            .MaximumLength(DirectMessage.MaxBodyLength).WithErrorCode("messages.body_too_long");
    }
}

/// <summary>
/// Birebir yazismanin acilma kosullari.
/// </summary>
/// <remarks>
/// Iki kapi var ve biri yeterli:
/// <list type="bullet">
/// <item>Kabul edilmis arkadaslik — arkadaslik yalnizca ogrenciler arasinda
/// kuruldugu icin bu kapi ogrenci-ogrenci yazismasini karsilar.</item>
/// <item>Ortak ders gecmisi — ogrenci, dersini aldigi egitmene yazabilir.
/// Tek bir rezervasyon yeterlidir ve kanal sonrasinda kapanmaz.</item>
/// </list>
/// </remarks>
internal static class MessagingAccess
{
    public static async Task<Result> EnsureCanMessageAsync(
        IDirectMessageRepository messages,
        Guid userId,
        Guid peerUserId,
        CancellationToken cancellationToken)
    {
        if (userId == peerUserId)
        {
            return MessageErrors.CannotMessageSelf;
        }

        // Konusma listesi filtresiyle ayni sorgu: kural tek yerde durur.
        var reachable = await messages.ReachablePeerIdsAsync(
            userId, [peerUserId], cancellationToken);

        return reachable.Contains(peerUserId)
            ? Result.Success()
            : MessageErrors.NotReachable;
    }
}

public sealed class ListConversationsHandler(
    IDirectMessageRepository messages,
    ICurrentUser currentUser)
{
    public async Task<Result<IReadOnlyList<ConversationListItem>>> Handle(
        CancellationToken cancellationToken)
    {
        if (currentUser.UserId is not { } userId)
        {
            return Error.Unauthorized("common.unauthorized");
        }

        var conversations = await messages.ListConversationsAsync(userId, cancellationToken);
        return conversations
            .Select(conversation => new ConversationListItem(
                conversation.PeerUserId,
                conversation.Email,
                conversation.FirstName,
                conversation.LastName,
                conversation.LastMessageBody,
                conversation.LastMessageAt,
                conversation.LastMessageFromMe,
                conversation.UnreadCount))
            .ToList();
    }
}

public sealed class CountUnreadMessagesHandler(
    IDirectMessageRepository messages,
    ICurrentUser currentUser)
{
    public async Task<Result<UnreadMessageCount>> Handle(CancellationToken cancellationToken)
    {
        if (currentUser.UserId is not { } userId)
        {
            return Error.Unauthorized("common.unauthorized");
        }

        return new UnreadMessageCount(await messages.CountUnreadAsync(userId, cancellationToken));
    }
}

/// <summary>
/// Karsi tarafla olan yazismayi doner ve ayni istekte okundu isaretler.
/// </summary>
/// <remarks>
/// Okundu isaretlemesi ayri bir uca birakilmadi: istemcinin ikinci bir cagri
/// yapmayi unutmasi rozetin sonsuza kadar yanmasina yol acardi. Donen mesajlar
/// isaretlemeden once okundugu icin, yanit kullanicinin ekranda gordugu son
/// durumu yansitir.
/// </remarks>
public sealed class GetMessageThreadHandler(
    IDirectMessageRepository messages,
    IUserRepository users,
    ICurrentUser currentUser,
    IUnitOfWork unitOfWork,
    IClock clock)
{
    private const int MessageLimit = 200;

    public async Task<Result<MessageThread>> Handle(
        Guid peerUserId,
        CancellationToken cancellationToken)
    {
        if (currentUser.UserId is not { } userId)
        {
            return Error.Unauthorized("common.unauthorized");
        }

        var peer = await users.FindByIdAsync(peerUserId, cancellationToken);
        if (peer is null)
        {
            return MessageErrors.UserNotFound;
        }

        var access = await MessagingAccess.EnsureCanMessageAsync(
            messages, userId, peerUserId, cancellationToken);
        if (access.IsFailure)
        {
            return access.Error;
        }

        var thread = await messages.ListThreadAsync(
            userId, peerUserId, MessageLimit, cancellationToken);

        var marked = await messages.MarkThreadReadAsync(
            userId, peerUserId, clock.UtcNow, cancellationToken);
        if (marked > 0)
        {
            await unitOfWork.SaveChangesAsync(cancellationToken);
        }

        return new MessageThread(
            peer.Id,
            peer.FirstName,
            peer.LastName,
            thread
                .Select(item => new MessageListItem(
                    item.MessageId,
                    item.SenderUserId,
                    item.SenderUserId == userId,
                    item.Body,
                    item.SentAt,
                    item.ReadAt))
                .ToList());
    }
}

public sealed class SendMessageHandler(
    IDirectMessageRepository messages,
    IUserRepository users,
    ICurrentUser currentUser,
    IUnitOfWork unitOfWork,
    IClock clock)
{
    public async Task<Result<MessageListItem>> Handle(
        SendMessageCommand command,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);
        if (currentUser.UserId is not { } userId)
        {
            return Error.Unauthorized("common.unauthorized");
        }

        var recipient = await users.FindByIdAsync(command.RecipientUserId, cancellationToken);
        if (recipient is null)
        {
            return MessageErrors.UserNotFound;
        }

        var access = await MessagingAccess.EnsureCanMessageAsync(
            messages, userId, recipient.Id, cancellationToken);
        if (access.IsFailure)
        {
            return access.Error;
        }

        var message = DirectMessage.Send(userId, recipient.Id, command.Body, clock.UtcNow);
        messages.Add(message);
        await unitOfWork.SaveChangesAsync(cancellationToken);

        return new MessageListItem(
            message.Id,
            message.SenderUserId,
            true,
            message.Body,
            message.SentAt,
            message.ReadAt);
    }
}
