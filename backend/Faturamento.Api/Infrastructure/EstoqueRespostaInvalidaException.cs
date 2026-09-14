namespace Faturamento.Api.Infrastructure;

/// <summary>
/// Estoque answered something this service does not know how to interpret. It deliberately does
/// not derive from DominioException: it becomes a 500 with a correlationId, because it is not a
/// business rule but a broken contract.
/// </summary>
public sealed class EstoqueRespostaInvalidaException(string message) : Exception(message);
