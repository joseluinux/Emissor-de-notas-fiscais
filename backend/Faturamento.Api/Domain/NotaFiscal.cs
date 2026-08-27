namespace Faturamento.Api.Domain;

public class NotaFiscal
{
    public int Id { get; set; }

    /// <summary>Sequencial vindo da sequence do Postgres, nunca calculado na aplicacao.</summary>
    public int Numero { get; set; }

    public StatusNotaFiscal Status { get; set; } = StatusNotaFiscal.Aberta;

    public DateTime CriadaEm { get; set; }

    public DateTime? ImpressaEm { get; set; }

    public List<NotaFiscalItem> Itens { get; set; } = [];
}
