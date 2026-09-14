using System.Diagnostics;
using Faturamento.Api.Domain;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;

namespace Faturamento.Api.Infrastructure;

/// <summary>
/// Turns any exception escaping the controllers into ProblemDetails (RFC 7807). Domain refusals
/// become 4xx with a readable detail; everything else becomes a 500 carrying a correlationId that
/// also appears in the server log. Stack traces only in the Development environment.
/// </summary>
/// <remarks>
/// TODO(revisar): Estoque splits the same job across two handlers (DomainExceptionHandler declines
/// what it does not recognise, UnhandledExceptionHandler is the backstop) while this service keeps
/// both branches in one class. Was the single handler a deliberate choice for the smaller surface
/// here, or just the older of the two designs?
/// </remarks>
public class DominioExceptionHandler(
    IProblemDetailsService problemDetailsService,
    IHostEnvironment environment,
    ILogger<DominioExceptionHandler> logger) : IExceptionHandler
{
    /// <inheritdoc />
    public async ValueTask<bool> TryHandleAsync(
        HttpContext httpContext,
        Exception exception,
        CancellationToken cancellationToken)
    {
        var correlationId = Activity.Current?.Id ?? httpContext.TraceIdentifier;

        ProblemDetails problemDetails;

        if (exception is DominioException dominio)
        {
            // Warning, not error: a refused request is an expected outcome, not a fault of the service.
            logger.LogWarning(
                dominio,
                "Regra de negocio rejeitou a requisicao. CorrelationId={CorrelationId}",
                correlationId);

            problemDetails = new ProblemDetails
            {
                Status = dominio.StatusCode,
                Title = "Requisicao rejeitada",
                Detail = dominio.Message
            };
        }
        else
        {
            logger.LogError(
                exception,
                "Erro nao tratado. CorrelationId={CorrelationId}",
                correlationId);

            // The detail stays generic on purpose: the correlationId is what connects the user's
            // report to the log entry, so nothing internal has to be put on the wire.
            problemDetails = new ProblemDetails
            {
                Status = StatusCodes.Status500InternalServerError,
                Title = "Erro interno",
                Detail = "Ocorreu um erro inesperado. Informe o correlationId para localizar o log."
            };

            if (environment.IsDevelopment())
            {
                problemDetails.Extensions["exception"] = exception.ToString();
            }
        }

        problemDetails.Extensions["correlationId"] = correlationId;
        httpContext.Response.StatusCode = problemDetails.Status!.Value;

        return await problemDetailsService.TryWriteAsync(new ProblemDetailsContext
        {
            HttpContext = httpContext,
            ProblemDetails = problemDetails,
            Exception = exception
        });
    }
}
