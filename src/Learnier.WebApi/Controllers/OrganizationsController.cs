using Learnier.Application.Common.Security;
using Learnier.Application.Features.Organizations.Commands.AssignRole;
using Learnier.Application.Features.Organizations.Commands.CreateOrganization;
using Learnier.Application.Features.Organizations.Commands.InviteMember;
using Learnier.Application.Features.Organizations.Commands.RemoveRole;
using Learnier.Application.Features.Organizations.Queries;
using Learnier.WebApi.Common;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Learnier.WebApi.Controllers;

/// <summary>
/// Organizasyon ve uyelik yonetimi.
/// </summary>
/// <remarks>
/// Uye islemleri aktif organizasyon kapsaminda calisir; kurum
/// <c>X-Organization-Id</c> basligiyla belirtilir.
/// </remarks>
[ApiController]
[Route("api/v1/organizations")]
[Authorize]
public sealed class OrganizationsController : ControllerBase
{
    [HttpGet("members")]
    [Authorize(Policy = Permissions.Organization.MemberManage)]
    public async Task<ActionResult<IReadOnlyList<OrganizationMemberListItem>>> ListMembers(
        [FromServices] ListOrganizationMembersHandler handler,
        CancellationToken cancellationToken)
        => (await handler.Handle(cancellationToken)).ToActionResult(this);

    [HttpGet("roles")]
    [Authorize(Policy = Permissions.Organization.MemberManage)]
    public async Task<ActionResult<IReadOnlyList<OrganizationRoleListItem>>> ListRoles(
        [FromServices] ListOrganizationRolesHandler handler,
        CancellationToken cancellationToken)
        => (await handler.Handle(cancellationToken)).ToActionResult(this);

    /// <summary>
    /// Yeni organizasyon olusturur. Kurucu otomatik olarak sahip rolunu alir.
    /// </summary>
    /// <remarks>
    /// Bu uc izin kontrolune tabi degil, yalnizca kimlik dogrulamasi ister:
    /// kullanici henuz hicbir organizasyona uye olmadigi icin bir izni de olamaz.
    /// </remarks>
    [HttpPost]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<CreateOrganizationResult>> Create(
        CreateOrganizationCommand command,
        [FromServices] CreateOrganizationHandler handler,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(handler);

        var result = await handler.Handle(command, cancellationToken);

        return result.ToActionResult(this);
    }

    /// <summary>
    /// Kayitli bir kullaniciyi aktif organizasyona davet eder.
    /// </summary>
    [HttpPost("members")]
    [Authorize(Policy = Permissions.Organization.MemberManage)]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<InviteMemberResult>> InviteMember(
        InviteMemberCommand command,
        [FromServices] InviteMemberHandler handler,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(handler);

        var result = await handler.Handle(command, cancellationToken);

        return result.ToActionResult(this);
    }

    /// <summary>
    /// Bir uyelige rol ekler.
    /// </summary>
    [HttpPost("members/{membershipId:guid}/roles")]
    [Authorize(Policy = Permissions.Organization.MemberManage)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult> AssignRole(
        Guid membershipId,
        AssignRoleRequest request,
        [FromServices] AssignRoleHandler handler,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(handler);
        ArgumentNullException.ThrowIfNull(request);

        var result = await handler.Handle(
            new AssignRoleCommand(membershipId, request.RoleId),
            cancellationToken);

        return result.ToActionResult(this);
    }

    /// <summary>Bir uyelikten atanmis rolu kaldirir.</summary>
    [HttpDelete("members/{membershipId:guid}/roles/{roleId:guid}")]
    [Authorize(Policy = Permissions.Organization.MemberManage)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult> RemoveRole(
        Guid membershipId,
        Guid roleId,
        [FromServices] RemoveRoleHandler handler,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(handler);

        var result = await handler.Handle(new RemoveRoleCommand(membershipId, roleId), cancellationToken);

        return result.ToActionResult(this);
    }
}

/// <summary>
/// Uyelik kimligi rotadan geldigi icin govde yalnizca rolu tasir.
/// </summary>
public sealed record AssignRoleRequest(Guid RoleId);
