namespace Faturamento.Api.Domain;

/// <summary>
/// O brief e explicito: nao permitir a impressao de notas com status diferente de Aberta.
/// Reimprimir nao e idempotencia — a idempotencia mora na movimentacao do Estoque, que replica
/// o resultado da baixa em vez de debitar de novo.
/// </summary>
public sealed class NotaNaoAbertaException(NotaFiscal nota)
    : DominioException(
        $"A nota {nota.Numero} esta {nota.Status} e apenas notas Abertas podem ser impressas.",
        StatusCodes.Status409Conflict);
