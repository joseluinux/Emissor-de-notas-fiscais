namespace Faturamento.Api.Domain;

/// <summary>
/// Base das rejeicoes de regra de negocio. O <see cref="StatusCode"/> vira o status da
/// resposta ProblemDetails; a mensagem vira o campo detail, entao ela e legivel pelo usuario.
/// </summary>
public abstract class DominioException(string message, int statusCode) : Exception(message)
{
    public int StatusCode { get; } = statusCode;
}
