using Learnier.Application.Common.Abstractions;
using Learnier.Application.Common.Results;
using Learnier.Domain.Billing;
using Learnier.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Learnier.Infrastructure.Billing;

internal sealed class CancellationPolicyService(
    AppDbContext context,
    ICurrentTenant currentTenant) : ICancellationPolicyService
{
    public async Task<Result<CancellationPolicySnapshot>> GetCurrentAsync(
        CancellationToken cancellationToken)
    {
        if (currentTenant.OrganizationId is not { } organizationId)
        {
            return Error.Validation("scheduling.organization_context_required");
        }

        var policy = await context.CancellationPolicies
            .SingleOrDefaultAsync(item => item.OrganizationId == organizationId, cancellationToken);

        return policy is null
            ? new CancellationPolicySnapshot(
                CancellationPolicy.DefaultStudentRefundCutoffMinutes,
                CancellationPolicy.DefaultInstructorPenaltyCutoffMinutes,
                1)
            : ToSnapshot(policy);
    }

    public async Task<Result<CancellationPolicySnapshot>> ConfigureAsync(
        int studentRefundCutoffMinutes,
        int instructorPenaltyCutoffMinutes,
        CancellationToken cancellationToken)
    {
        if (currentTenant.OrganizationId is not { } organizationId)
        {
            return Error.Validation("scheduling.organization_context_required");
        }

        var policy = await context.CancellationPolicies
            .SingleOrDefaultAsync(item => item.OrganizationId == organizationId, cancellationToken);

        if (policy is null)
        {
            policy = CancellationPolicy.Create(
                organizationId,
                studentRefundCutoffMinutes,
                instructorPenaltyCutoffMinutes);
            context.CancellationPolicies.Add(policy);
        }
        else
        {
            policy.Update(studentRefundCutoffMinutes, instructorPenaltyCutoffMinutes);
        }

        return ToSnapshot(policy);
    }

    private static CancellationPolicySnapshot ToSnapshot(CancellationPolicy policy)
        => new(
            policy.StudentRefundCutoffMinutes,
            policy.InstructorPenaltyCutoffMinutes,
            policy.Version);
}
