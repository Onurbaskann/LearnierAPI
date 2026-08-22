using FluentValidation;
using Learnier.Application.Common.Abstractions;
using Learnier.Application.Common.Results;
using Learnier.Domain.Scheduling;

namespace Learnier.Application.Features.Scheduling.Commands.CreateClassGroup;

/// <param name="DeliveryType">
/// <c>Cohort</c> sabit kadro, <c>DropInPool</c> serbest katilim havuzu.
/// </param>
public sealed record CreateClassGroupCommand(
    Guid CourseId,
    string Name,
    ClassGroupDeliveryType DeliveryType,
    int Capacity,
    DateOnly? StartsOn = null,
    DateOnly? EndsOn = null);

public sealed record CreateClassGroupResult(Guid ClassGroupId, ClassGroupStatus Status);

internal sealed class CreateClassGroupValidator : AbstractValidator<CreateClassGroupCommand>
{
    public CreateClassGroupValidator()
    {
        RuleFor(c => c.CourseId)
            .NotEmpty().WithErrorCode("scheduling.course_required");

        RuleFor(c => c.Name)
            .NotEmpty().WithErrorCode("scheduling.class_group_name_required")
            .MaximumLength(200).WithErrorCode("scheduling.class_group_name_too_long");

        RuleFor(c => c.DeliveryType)
            .IsInEnum().WithErrorCode("scheduling.delivery_type_invalid");

        RuleFor(c => c.Capacity)
            .InclusiveBetween(1, 1000).WithErrorCode("scheduling.capacity_invalid");

        RuleFor(c => c.EndsOn)
            .GreaterThanOrEqualTo(c => c.StartsOn!.Value)
            .WithErrorCode("scheduling.date_range_invalid")
            .When(c => c.StartsOn is not null && c.EndsOn is not null);
    }
}

/// <summary>
/// Bir egitim icin sinif olusturur.
/// </summary>
public sealed class CreateClassGroupHandler(
    ISchedulingRepository scheduling,
    ICatalogRepository catalog,
    ICurrentTenant currentTenant,
    IUnitOfWork unitOfWork)
{
    public async Task<Result<CreateClassGroupResult>> Handle(
        CreateClassGroupCommand command,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        if (currentTenant.OrganizationId is not { } organizationId)
        {
            return SchedulingErrors.OrganizationContextRequired;
        }

        // Egitim sorgusu kiraci filtresine tabi: baska kurumun egitimine sinif acilamaz.
        var course = await catalog.FindCourseAsync(command.CourseId, includeModules: false, cancellationToken);

        if (course is null)
        {
            return SchedulingErrors.CourseNotFound;
        }

        var classGroup = ClassGroup.Create(
            organizationId,
            course.Id,
            command.Name,
            command.DeliveryType,
            command.Capacity,
            command.StartsOn,
            command.EndsOn);

        scheduling.AddClassGroup(classGroup);
        await unitOfWork.SaveChangesAsync(cancellationToken);

        return new CreateClassGroupResult(classGroup.Id, classGroup.Status);
    }
}
