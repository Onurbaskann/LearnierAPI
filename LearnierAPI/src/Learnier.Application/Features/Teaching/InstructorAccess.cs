using Learnier.Application.Common.Abstractions;
using Learnier.Application.Common.Results;
using Learnier.Domain.Teaching;

namespace Learnier.Application.Features.Teaching;

/// <summary>
/// Egitmen profiline yazma erisiminin kurali.
/// </summary>
/// <remarks>
/// <para>
/// Iki durumda izin verilir: cagirici profilin sahibiyse (egitmen kendi biyografisini
/// ve programini yonetir) veya egitmenleri yonetme yetkisi varsa.
/// </para>
/// <para>
/// Yetki bilgisi caller'dan <c>canManageInstructors</c> olarak gelir; izin cozumlemesi
/// WebApi katmaninin isi, Application yalnizca sonucu kullanir.
/// </para>
/// </remarks>
internal static class InstructorAccess
{
    public static Error? Check(
        InstructorProfile profile,
        ICurrentTenant currentTenant,
        bool canManageInstructors)
    {
        if (canManageInstructors)
        {
            return null;
        }

        return currentTenant.MembershipId == profile.MembershipId
            ? null
            : TeachingErrors.ProfileNotOwned;
    }
}
