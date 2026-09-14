using System.ComponentModel.DataAnnotations;

namespace Estoque.Api.Dtos;

/// <summary>A stock debit request. Products are addressed by code, never by this service's Id.</summary>
public sealed class CriarMovimentacaoRequest
{
    /// <summary>Caller's natural key — repeating it replays the original result instead of debiting twice.</summary>
    [Required(ErrorMessage = "Referência é obrigatória."), MaxLength(80)]
    public string Referencia { get; init; } = string.Empty;

    [Required, MinLength(1, ErrorMessage = "Informe ao menos um item.")]
    public List<MovimentacaoItemRequest> Itens { get; init; } = [];
}

/// <summary>One line of a debit. Repeating a product across lines is allowed; the service sums them.</summary>
public sealed class MovimentacaoItemRequest
{
    [Required(ErrorMessage = "Código do produto é obrigatório."), MaxLength(40)]
    public string ProdutoCodigo { get; init; } = string.Empty;

    [Range(1, int.MaxValue, ErrorMessage = "Quantidade deve ser maior que zero.")]
    public int Quantidade { get; init; }
}

/// <summary>A recorded movement. A replay returns this byte-identical to the original call.</summary>
public sealed record MovimentacaoResponse(
    int Id,
    string Referencia,
    DateTime CriadaEm,
    List<MovimentacaoItemResponse> Itens);

/// <summary>One debited line and the balance it left behind.</summary>
public sealed record MovimentacaoItemResponse(string ProdutoCodigo, int Quantidade, int SaldoResultante);
