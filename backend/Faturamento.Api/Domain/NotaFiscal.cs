namespace Faturamento.Api.Domain;

/// <summary>
/// An invoice. It is created Aberta and only printing closes it; <see cref="ImpressaEm"/> stays
/// null until then.
/// </summary>
public class NotaFiscal
{
    public int Id { get; set; }

    /// <summary>Sequential number from the Postgres sequence, never computed in the application.</summary>
    public int Numero { get; set; }

    public StatusNotaFiscal Status { get; set; } = StatusNotaFiscal.Aberta;

    public DateTime CriadaEm { get; set; }

    public DateTime? ImpressaEm { get; set; }

    public List<NotaFiscalItem> Itens { get; set; } = [];
}
