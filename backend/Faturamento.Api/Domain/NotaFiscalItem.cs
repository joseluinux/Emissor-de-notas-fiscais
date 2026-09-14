namespace Faturamento.Api.Domain;

/// <summary>One product line of an invoice.</summary>
public class NotaFiscalItem
{
    public int Id { get; set; }

    public int NotaFiscalId { get; set; }

    /// <summary>The product's business key in Estoque. The invoice never stores the other service's Id.</summary>
    public string ProdutoCodigo { get; set; } = string.Empty;

    /// <summary>Snapshot of the description at issue time.</summary>
    public string Descricao { get; set; } = string.Empty;

    public int Quantidade { get; set; }
}
