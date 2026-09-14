namespace Estoque.Api.Domain;

/// <summary>A registered product and the balance available for invoices to consume.</summary>
public class Produto
{
    public int Id { get; set; }

    /// <summary>Business key used by other services; never expose the surrogate Id across the wire.</summary>
    public string Codigo { get; set; } = string.Empty;

    public string Descricao { get; set; } = string.Empty;

    public int Saldo { get; set; }

    /// <summary>Mapped to PostgreSQL's xmin system column, so simultaneous debits conflict instead of overwriting.</summary>
    public uint Version { get; set; }
}
