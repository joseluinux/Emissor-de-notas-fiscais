namespace Faturamento.Api.Infrastructure;

// Copia local do contrato do Estoque. Os dois servicos sao independentes: nada de projeto
// compartilhado, cada lado carrega a sua propria versao destes tipos.

/// <param name="Referencia">
/// Chave natural da operacao. Repetir a mesma referencia replica o resultado original em vez
/// de debitar de novo — e o que protege contra baixa dupla quando a impressao e reexecutada.
/// </param>
public record RegistrarMovimentacaoRequest(
    string Referencia,
    IReadOnlyList<MovimentacaoItemRequest> Itens);

public record MovimentacaoItemRequest(string ProdutoCodigo, int Quantidade);

public record MovimentacaoEstoqueResponse(
    int Id,
    string Referencia,
    DateTime CriadaEm,
    IReadOnlyList<MovimentacaoEstoqueItemResponse> Itens);

public record MovimentacaoEstoqueItemResponse(string ProdutoCodigo, int Quantidade, int SaldoResultante);
