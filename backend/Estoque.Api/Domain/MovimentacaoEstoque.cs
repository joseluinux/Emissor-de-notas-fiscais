namespace Estoque.Api.Domain;

/// <summary>
/// A single all-or-nothing stock debit. <see cref="Referencia"/> is the caller's natural key
/// (an invoice, for example) and carries a unique index, which is what makes the operation
/// idempotent: replaying a reference returns the stored result instead of debiting again.
/// </summary>
public class MovimentacaoEstoque
{
    public int Id { get; set; }

    public string Referencia { get; set; } = string.Empty;

    public DateTime CriadaEm { get; set; }

    public List<MovimentacaoEstoqueItem> Itens { get; set; } = [];
}
