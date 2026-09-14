namespace Faturamento.Api.Infrastructure;

/// <summary>
/// The only way into Estoque. An interface so the print flow can be tested without a live stock
/// service on the other end.
/// </summary>
public interface IEstoqueClient
{
    /// <summary>Debits Estoque for every item in one call, all-or-nothing.</summary>
    /// <exception cref="Domain.EstoqueRecusouException">Estoque refused on a business rule.</exception>
    /// <exception cref="EstoqueRespostaInvalidaException">Estoque answered something unexpected.</exception>
    Task<MovimentacaoEstoqueResponse> RegistrarBaixaAsync(
        RegistrarMovimentacaoRequest request,
        CancellationToken cancellationToken);
}
