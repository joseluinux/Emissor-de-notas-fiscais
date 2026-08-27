using System.ComponentModel.DataAnnotations;

namespace Estoque.Api.Dtos;

public sealed class CriarMovimentacaoRequest
{
    /// <summary>Caller's natural key — repeating it replays the original result instead of debiting twice.</summary>
    [Required(ErrorMessage = "Referência é obrigatória."), MaxLength(80)]
    public string Referencia { get; init; } = string.Empty;

    [Required, MinLength(1, ErrorMessage = "Informe ao menos um item.")]
    public List<MovimentacaoItemRequest> Itens { get; init; } = [];
}

public sealed class MovimentacaoItemRequest
{
    [Required(ErrorMessage = "Código do produto é obrigatório."), MaxLength(40)]
    public string ProdutoCodigo { get; init; } = string.Empty;

    [Range(1, int.MaxValue, ErrorMessage = "Quantidade deve ser maior que zero.")]
    public int Quantidade { get; init; }
}

public sealed record MovimentacaoResponse(
    int Id,
    string Referencia,
    DateTime CriadaEm,
    List<MovimentacaoItemResponse> Itens);

public sealed record MovimentacaoItemResponse(string ProdutoCodigo, int Quantidade, int SaldoResultante);
