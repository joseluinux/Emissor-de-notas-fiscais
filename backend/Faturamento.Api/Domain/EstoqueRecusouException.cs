namespace Faturamento.Api.Domain;

/// <summary>
/// Estoque refused the debit on a business rule (insufficient balance, unknown product or a
/// concurrency conflict). The status and the detail travel through intact, so the user reads
/// exactly which product fell short without Faturamento rewriting the message.
/// </summary>
/// <remarks>
/// A domain exception rather than an infrastructure one: the call itself worked, the answer was
/// simply "no". Compare <see cref="Infrastructure.EstoqueRespostaInvalidaException"/>.
/// </remarks>
public sealed class EstoqueRecusouException(int statusCode, string detalhe)
    : DominioException(detalhe, statusCode);
