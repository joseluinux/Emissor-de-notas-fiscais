namespace Faturamento.Api.Domain;

/// <summary>
/// The requested invoice does not exist. Thrown rather than returned so the failure travels
/// through <c>DominioExceptionHandler</c> like every other refusal in this service: without it
/// the 404 body carries no <c>correlationId</c> and the server logs nothing at all, leaving a
/// support call with no line to search for.
/// </summary>
public sealed class NotaNaoEncontradaException(int id)
    : DominioException($"Nao existe nota fiscal com Id {id}.", StatusCodes.Status404NotFound);
