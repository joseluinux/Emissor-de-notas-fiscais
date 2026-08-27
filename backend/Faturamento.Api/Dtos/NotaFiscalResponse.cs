using Faturamento.Api.Domain;

namespace Faturamento.Api.Dtos;

public record NotaFiscalResponse(
    int Id,
    int Numero,
    string Status,
    DateTime CriadaEm,
    DateTime? ImpressaEm,
    IReadOnlyList<NotaFiscalItemResponse> Itens)
{
    public static NotaFiscalResponse De(NotaFiscal nota) => new(
        nota.Id,
        nota.Numero,
        nota.Status.ToString(),
        nota.CriadaEm,
        nota.ImpressaEm,
        nota.Itens
            .OrderBy(i => i.Id)
            .Select(i => new NotaFiscalItemResponse(i.Id, i.ProdutoCodigo, i.Descricao, i.Quantidade))
            .ToList());
}

public record NotaFiscalItemResponse(int Id, string ProdutoCodigo, string Descricao, int Quantidade);
