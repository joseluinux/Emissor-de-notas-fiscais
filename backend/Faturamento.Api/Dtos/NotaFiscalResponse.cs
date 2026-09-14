using Faturamento.Api.Domain;

namespace Faturamento.Api.Dtos;

/// <summary>An invoice as returned by the API. Status travels as text so clients never depend on the enum's ordinals.</summary>
public record NotaFiscalResponse(
    int Id,
    int Numero,
    string Status,
    DateTime CriadaEm,
    DateTime? ImpressaEm,
    IReadOnlyList<NotaFiscalItemResponse> Itens)
{
    /// <summary>Maps an entity to its response. Items are ordered by Id, so the payload is stable across calls.</summary>
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

/// <summary>One invoice line as returned by the API.</summary>
public record NotaFiscalItemResponse(int Id, string ProdutoCodigo, string Descricao, int Quantidade);
