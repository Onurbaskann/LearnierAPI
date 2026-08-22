using FluentValidation;
using Learnier.Application.Common.Results;
using Learnier.WebApi.Common;
using Learnier.WebApi.Localization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;

namespace Learnier.WebApi.Filters;

/// <summary>
/// Action parametreleri icin kayitli FluentValidation kurallarini calistirir.
/// </summary>
/// <remarks>
/// <para>
/// Mediator kutuphanesi kullanmadigimiz icin validation bir pipeline behavior'i degil,
/// ASP.NET Core'un kendi filter'i olarak calisir. Sonuc ayni: her istek handler'a
/// ulasmadan once dogrulanir, handler icinde tekrar eden kontrol kodu olmaz.
/// </para>
/// <para>
/// Kurallar mesaj degil <c>WithErrorCode</c> ile kod tasir; ceviri burada yapilir.
/// </para>
/// </remarks>
internal sealed class ValidationFilter(
    IServiceProvider serviceProvider,
    ErrorMessageResolver errorMessageResolver,
    Microsoft.Extensions.Localization.IStringLocalizer<ErrorMessages> localizer)
    : IAsyncActionFilter
{
    public async Task OnActionExecutionAsync(
        ActionExecutingContext context,
        ActionExecutionDelegate next)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(next);

        var failures = new Dictionary<string, List<string>>(StringComparer.Ordinal);

        foreach (var argument in context.ActionArguments.Values)
        {
            if (argument is null)
            {
                continue;
            }

            var validator = serviceProvider.GetService(
                typeof(IValidator<>).MakeGenericType(argument.GetType())) as IValidator;

            if (validator is null)
            {
                continue;
            }

            var validationContext = new ValidationContext<object>(argument);
            var result = await validator.ValidateAsync(validationContext, context.HttpContext.RequestAborted);

            foreach (var failure in result.Errors)
            {
                // ErrorCode bos birakilmissa kural mesaji kod olarak kullanilir;
                // bu durum kaynak dosyada karsiligi olmadigi icin ham metin olarak gorunur
                // ve eksik tanimi gorunur kilar.
                var code = string.IsNullOrWhiteSpace(failure.ErrorCode)
                    ? failure.ErrorMessage
                    : failure.ErrorCode;

                var message = errorMessageResolver.Resolve(Error.Validation(code));

                if (!failures.TryGetValue(failure.PropertyName, out var messages))
                {
                    messages = [];
                    failures[failure.PropertyName] = messages;
                }

                messages.Add(message);
            }
        }

        if (failures.Count > 0)
        {
            context.Result = BuildValidationProblem(context, failures);
            return;
        }

        await next();
    }

    private ObjectResult BuildValidationProblem(
        ActionExecutingContext context,
        Dictionary<string, List<string>> failures)
    {
        var problem = new ValidationProblemDetails(
            failures.ToDictionary(pair => pair.Key, pair => pair.Value.ToArray(), StringComparer.Ordinal))
        {
            Status = ErrorType.Validation.ToStatusCode(),
            Title = ErrorType.Validation.ToTitle(),
            Detail = localizer["common.validation_failed"],
            Instance = context.HttpContext.Request.Path
        };

        problem.Extensions["errorCode"] = "common.validation_failed";

        return new ObjectResult(problem) { StatusCode = problem.Status };
    }
}
