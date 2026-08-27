using System.Diagnostics;
using Faturamento.Api.Domain;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;

namespace Faturamento.Api.Infrastructure;

/// <summary>
/// Converte qualquer excecao que escape dos controllers em ProblemDetails (RFC 7807).
/// Rejeicoes de dominio viram 4xx com detail legivel; o resto vira 500 com um correlationId
/// que aparece no log do servidor. Stack trace so no ambiente de Development.
/// </summary>
public class DominioExceptionHandler(
    IProblemDetailsService problemDetailsService,
    IHostEnvironment environment,
    ILogger<DominioExceptionHandler> logger) : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(
        HttpContext httpContext,
        Exception exception,
        CancellationToken cancellationToken)
    {
        var correlationId = Activity.Current?.Id ?? httpContext.TraceIdentifier;

        ProblemDetails problemDetails;

        if (exception is DominioException dominio)
        {
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
