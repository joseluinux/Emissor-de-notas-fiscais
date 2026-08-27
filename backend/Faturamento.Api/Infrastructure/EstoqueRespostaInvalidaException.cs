namespace Faturamento.Api.Infrastructure;

/// <summary>
/// O Estoque respondeu algo que este servico nao sabe interpretar. Nao deriva de
/// DominioException de proposito: vira 500 com correlationId, porque nao e uma regra de
/// negocio, e sim um contrato quebrado.
/// </summary>
public sealed class EstoqueRespostaInvalidaException(string message) : Exception(message);
