using System.ComponentModel.DataAnnotations;

namespace Faturamento.Api.Dtos;

/// <summary>Payload for creating an invoice. Numero and Status are not accepted — the service sets both.</summary>
public record CriarNotaFiscalRequest
{
    [Required(ErrorMessage = "Informe ao menos um item.")]
    [MinLength(1, ErrorMessage = "Informe ao menos um item.")]
    public List<CriarNotaFiscalItemRequest> Itens { get; init; } = [];
}

/// <summary>
/// One line of a new invoice.
/// </summary>
/// <remarks>
/// TODO(revisar): the caller supplies Descricao, and nothing checks it against the product
/// registered in Estoque — neither at creation nor at print time, when only ProdutoCodigo and
/// Quantidade are sent over. Was trusting the caller's description deliberate (it is a snapshot,
/// and looking it up would mean an extra call to Estoque), or is a lookup missing?
/// </remarks>
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
