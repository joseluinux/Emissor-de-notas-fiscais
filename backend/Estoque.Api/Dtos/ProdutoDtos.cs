using System.ComponentModel.DataAnnotations;

namespace Estoque.Api.Dtos;

public sealed class CriarProdutoRequest
{
    [Required(ErrorMessage = "Código é obrigatório."), MaxLength(40)]
    public string Codigo { get; init; } = string.Empty;

    [Required(ErrorMessage = "Descrição é obrigatória."), MaxLength(200)]
    public string Descricao { get; init; } = string.Empty;

    [Range(0, int.MaxValue, ErrorMessage = "Saldo não pode ser negativo.")]
    public int Saldo { get; init; }
}

public sealed class AtualizarProdutoRequest
{
    [Required(ErrorMessage = "Descrição é obrigatória."), MaxLength(200)]
    public string Descricao { get; init; } = string.Empty;

    [Range(0, int.MaxValue, ErrorMessage = "Saldo não pode ser negativo.")]
    public int Saldo { get; init; }
}

public sealed record ProdutoResponse(int Id, string Codigo, string Descricao, int Saldo);
