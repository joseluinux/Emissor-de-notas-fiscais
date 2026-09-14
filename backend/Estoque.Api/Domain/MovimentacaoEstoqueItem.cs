namespace Estoque.Api.Domain;

/// <summary>One product line of a <see cref="MovimentacaoEstoque"/>.</summary>
public class MovimentacaoEstoqueItem
{
    public int Id { get; set; }

    public int MovimentacaoEstoqueId { get; set; }

    public int ProdutoId { get; set; }

    /// <summary>The code as sent by the caller, kept next to the foreign key so the audit trail reads on its own.</summary>
    public string ProdutoCodigo { get; set; } = string.Empty;

    public int Quantidade { get; set; }

    /// <summary>Balance left after this line was applied — snapshotted so the audit trail is readable on its own.</summary>
    public int SaldoResultante { get; set; }
}
