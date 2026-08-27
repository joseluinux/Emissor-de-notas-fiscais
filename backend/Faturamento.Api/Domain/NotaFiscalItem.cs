namespace Faturamento.Api.Domain;

public class NotaFiscalItem
{
    public int Id { get; set; }

    public int NotaFiscalId { get; set; }

    /// <summary>Chave de negocio do produto no Estoque. A nota nunca guarda o Id do outro servico.</summary>
    public string ProdutoCodigo { get; set; } = string.Empty;

    /// <summary>Snapshot da descricao no momento da emissao.</summary>
    public string Descricao { get; set; } = string.Empty;

    public int Quantidade { get; set; }
}
