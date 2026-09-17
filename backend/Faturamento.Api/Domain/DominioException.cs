namespace Faturamento.Api.Domain;

/// <summary>
/// Base for business-rule refusals. <see cref="StatusCode"/> becomes the status of the
/// ProblemDetails response and the message becomes its detail field, so the message is read by
/// the user — write it accordingly.
/// </summary>
public abstract class DominioException(string message, int statusCode, Exception? causa = null)
    : Exception(message, causa)
{
    public int StatusCode { get; } = statusCode;
}
