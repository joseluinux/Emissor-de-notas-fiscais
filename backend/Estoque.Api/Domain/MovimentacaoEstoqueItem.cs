namespace Estoque.Api.Domain;

public class MovimentacaoEstoqueItem
{
    public int Id { get; set; }

    public int MovimentacaoEstoqueId { get; set; }

    public int ProdutoId { get; set; }

    public string ProdutoCodigo { get; set; } = string.Empty;

    public int Quantidade { get; set; }

    /// <summary>Balance left after this line was applied — snapshotted so the audit trail is readable on its own.</summary>
    public int SaldoResultante { get; set; }
}
