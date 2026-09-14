using System.ComponentModel.DataAnnotations;

namespace Estoque.Api.Dtos;

/// <summary>Product registration payload. The code must be free; the balance is the opening one.</summary>
public sealed class CriarProdutoRequest
{
    [Required(ErrorMessage = "Código é obrigatório."), MaxLength(40)]
    public string Codigo { get; init; } = string.Empty;

    [Required(ErrorMessage = "Descrição é obrigatória."), MaxLength(200)]
    public string Descricao { get; init; } = string.Empty;

    [Range(0, int.MaxValue, ErrorMessage = "Saldo não pode ser negativo.")]
    public int Saldo { get; init; }
}

/// <summary>Correction to an existing product. The code cannot be changed through this endpoint, so it is not accepted here.</summary>
public sealed class AtualizarProdutoRequest
{
    [Required(ErrorMessage = "Descrição é obrigatória."), MaxLength(200)]
    public string Descricao { get; init; } = string.Empty;

    [Range(0, int.MaxValue, ErrorMessage = "Saldo não pode ser negativo.")]
    public int Saldo { get; init; }
}

/// <summary>A product as seen from outside; the xmin row version is never exposed.</summary>
public sealed record ProdutoResponse(int Id, string Codigo, string Descricao, int Saldo);
