using System.ComponentModel.DataAnnotations;

namespace Faturamento.Api.Dtos;

public record CriarNotaFiscalRequest
{
    [Required(ErrorMessage = "Informe ao menos um item.")]
    [MinLength(1, ErrorMessage = "Informe ao menos um item.")]
    public List<CriarNotaFiscalItemRequest> Itens { get; init; } = [];
}

public record CriarNotaFiscalItemRequest
{
    [Required(ErrorMessage = "O codigo do produto e obrigatorio.")]
    [MaxLength(50)]
    public string ProdutoCodigo { get; init; } = string.Empty;

    [Required(ErrorMessage = "A descricao do produto e obrigatoria.")]
    [MaxLength(200)]
    public string Descricao { get; init; } = string.Empty;

    [Range(1, int.MaxValue, ErrorMessage = "A quantidade deve ser maior que zero.")]
    public int Quantidade { get; init; }
}
