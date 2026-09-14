namespace Faturamento.Api.Domain;

/// <summary>
/// The brief is explicit: invoices whose status is not Aberta must not be printed. Reprinting is
/// not idempotency — idempotency lives in the Estoque movement, which replays the result of the
/// debit instead of debiting again.
/// </summary>
public sealed class NotaNaoAbertaException(NotaFiscal nota)
    : DominioException(
        $"A nota {nota.Numero} esta {nota.Status} e apenas notas Abertas podem ser impressas.",
        StatusCodes.Status409Conflict);
