namespace Faturamento.Api.Infrastructure;

// Local copy of the Estoque contract. The two services are independent: no shared project, each
// side carries its own version of these types.

/// <param name="Referencia">
/// Natural key of the operation. Repeating the same reference replays the original result instead
/// of debiting again — this is what protects against a double debit when a print is re-run.
/// </param>
public record RegistrarMovimentacaoRequest(
    string Referencia,
    IReadOnlyList<MovimentacaoItemRequest> Itens);

/// <summary>One line of the debit, addressing the product by code.</summary>
public record MovimentacaoItemRequest(string ProdutoCodigo, int Quantidade);

/// <summary>The movement Estoque recorded, whether this call debited it or replayed it.</summary>
public record MovimentacaoEstoqueResponse(
    int Id,
    string Referencia,
    DateTime CriadaEm,
    IReadOnlyList<MovimentacaoEstoqueItemResponse> Itens);

/// <summary>One debited line and the balance Estoque was left with.</summary>
public record MovimentacaoEstoqueItemResponse(string ProdutoCodigo, int Quantidade, int SaldoResultante);
