namespace Faturamento.Api.Infrastructure;

public interface IEstoqueClient
{
    /// <summary>Debita o Estoque para todos os itens de uma vez, tudo ou nada.</summary>
    /// <exception cref="Domain.EstoqueRecusouException">O Estoque recusou por regra de negocio.</exception>
    /// <exception cref="EstoqueRespostaInvalidaException">O Estoque respondeu algo inesperado.</exception>
    Task<MovimentacaoEstoqueResponse> RegistrarBaixaAsync(
        RegistrarMovimentacaoRequest request,
        CancellationToken cancellationToken);
}
