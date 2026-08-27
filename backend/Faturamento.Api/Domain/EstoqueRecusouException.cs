namespace Faturamento.Api.Domain;

/// <summary>
/// O Estoque recusou a baixa por regra de negocio (saldo insuficiente, produto inexistente ou
/// conflito de concorrencia). O status e o detail atravessam intactos, entao o usuario le
/// exatamente qual produto faltou, sem que o Faturamento reescreva a mensagem.
/// </summary>
public sealed class EstoqueRecusouException(int statusCode, string detalhe)
    : DominioException(detalhe, statusCode);
