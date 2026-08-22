using System.Text;
using Learnier.Application.Common.Abstractions;
using Learnier.Application.Features.Billing.Commands.CreateCheckout;
using Learnier.Application.Features.Billing.Commands.ProcessPaymentWebhook;
using Learnier.Infrastructure.Billing;
using Learnier.WebApi.Common;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Learnier.WebApi.Controllers;

[ApiController]
[Route("api/v1/payments")]
public sealed class PaymentsController : ControllerBase
{
    /// <summary>
    /// Aktif plan fiyatindan saglayici checkout oturumu acar. Bu adim abonelik veya
    /// ders hakki vermez; aktivasyon dogrulanmis webhook ile yapilir.
    /// </summary>
    [Authorize]
    [HttpPost("checkouts")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<CreateCheckoutResult>> CreateCheckout(
        CreateCheckoutCommand command,
        [FromServices] CreateCheckoutHandler handler,
        CancellationToken cancellationToken)
        => (await handler.Handle(command, cancellationToken)).ToActionResult(this);

    /// <summary>
    /// Saglayicinin ham webhook govdesini imza dogrulamasindan sonra idempotent isler.
    /// </summary>
    [AllowAnonymous]
    [HttpPost("webhooks/{provider}")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<ProcessPaymentWebhookResult>> Webhook(
        string provider,
        [FromServices] ProcessPaymentWebhookHandler handler,
        CancellationToken cancellationToken)
    {
        Request.EnableBuffering();
        using var reader = new StreamReader(
            Request.Body,
            Encoding.UTF8,
            detectEncodingFromByteOrderMarks: false,
            leaveOpen: true);
        var payload = await reader.ReadToEndAsync(cancellationToken);
        Request.Body.Position = 0;

        var headers = Request.Headers.ToDictionary(
            header => header.Key,
            header => header.Value.ToString(),
            StringComparer.OrdinalIgnoreCase);

        return (await handler.Handle(provider, payload, headers, cancellationToken))
            .ToActionResult(this);
    }

    /// <summary>
    /// Yalnizca Development ortaminda sandbox checkout'u basarili odeme gibi tamamlar.
    /// </summary>
    [Authorize]
    [HttpPost("sandbox/checkouts/{checkoutSessionId:guid}/complete")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ProcessPaymentWebhookResult>> CompleteSandboxCheckout(
        Guid checkoutSessionId,
        [FromServices] IWebHostEnvironment environment,
        [FromServices] SandboxPaymentProvider provider,
        [FromServices] IPaymentOrchestrationRepository repository,
        [FromServices] ICurrentUser currentUser,
        [FromServices] IClock clock,
        [FromServices] ProcessPaymentWebhookHandler handler,
        CancellationToken cancellationToken)
    {
        if (!environment.IsDevelopment())
        {
            return NotFound();
        }

        var checkout = await repository.FindCheckoutAsync(checkoutSessionId, cancellationToken);
        if (checkout is null || checkout.UserId != currentUser.UserId)
        {
            return NotFound();
        }

        var signedWebhook = provider.CreateSuccessfulWebhook(checkout, clock.UtcNow);
        var headers = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            [SandboxPaymentProvider.SignatureHeader] = signedWebhook.Signature
        };

        return (await handler.Handle(
                provider.Name,
                signedWebhook.Payload,
                headers,
                cancellationToken))
            .ToActionResult(this);
    }
}
