using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;

namespace Estoque.Api.Errors;

/// <summary>Maps expected business refusals to ProblemDetails. Runs before <see cref="UnhandledExceptionHandler"/>.</summary>
public sealed class DomainExceptionHandler(IProblemDetailsService problemDetailsService) : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(
        HttpContext httpContext,
        Exception exception,
        CancellationToken cancellationToken)
    {
        if (exception is not DomainException domainException)
        {
            return false;
        }

        httpContext.Response.StatusCode = domainException.StatusCode;

        var problemDetails = new ProblemDetails
        {
            Status = domainException.StatusCode,
            Title = domainException.Title,
            Detail = domainException.Message,
        };

        foreach (var (chave, valor) in domainException.Extensions)
        {
            problemDetails.Extensions[chave] = valor;
        }

        return await problemDetailsService.TryWriteAsync(new ProblemDetailsContext
        {
            HttpContext = httpContext,
            Exception = exception,
            ProblemDetails = problemDetails,
        });
    }
}
