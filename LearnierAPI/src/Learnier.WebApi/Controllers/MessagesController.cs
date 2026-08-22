using Learnier.Application.Features.Messages;
using Learnier.WebApi.Common;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Learnier.WebApi.Controllers;

/// <summary>
/// Ogrenciler arasi birebir mesajlasma. Yazisma yalnizca kabul edilmis
/// arkadasliklar uzerinden acilir; yetki kontrolu handler'lardadir.
/// </summary>
[ApiController]
[Route("api/v1/messages")]
[Authorize]
public sealed class MessagesController : ControllerBase
{
    [HttpGet("conversations")]
    public async Task<ActionResult<IReadOnlyList<ConversationListItem>>> ListConversations(
        [FromServices] ListConversationsHandler handler,
        CancellationToken cancellationToken)
        => (await handler.Handle(cancellationToken)).ToActionResult(this);

    /// <summary>Kenar cubugundaki rozet bunu periyodik olarak yoklar.</summary>
    [HttpGet("unread-count")]
    public async Task<ActionResult<UnreadMessageCount>> CountUnread(
        [FromServices] CountUnreadMessagesHandler handler,
        CancellationToken cancellationToken)
        => (await handler.Handle(cancellationToken)).ToActionResult(this);

    /// <summary>Yazismayi doner ve gelen mesajlari okundu isaretler.</summary>
    [HttpGet("{peerUserId:guid}")]
    public async Task<ActionResult<MessageThread>> GetThread(
        Guid peerUserId,
        [FromServices] GetMessageThreadHandler handler,
        CancellationToken cancellationToken)
        => (await handler.Handle(peerUserId, cancellationToken)).ToActionResult(this);

    [HttpPost]
    public async Task<ActionResult<MessageListItem>> Send(
        SendMessageCommand command,
        [FromServices] SendMessageHandler handler,
        CancellationToken cancellationToken)
        => (await handler.Handle(command, cancellationToken)).ToActionResult(this);
}
