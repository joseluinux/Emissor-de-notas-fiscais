using Estoque.Api.Dtos;
using Estoque.Api.Errors;
using Estoque.Api.Services;
using Microsoft.AspNetCore.Mvc;

namespace Estoque.Api.Controllers;

[ApiController]
[Route("api/estoque/movimentacoes")]
public class MovimentacoesEstoqueController(MovimentacaoEstoqueService servico) : ControllerBase
{
    /// <summary>Debits stock for every line, all-or-nothing.</summary>
    /// <remarks>
    /// Idempotent on <c>referencia</c>: 201 the first time, 200 with the original result on every
    /// repeat, and nothing is debited twice.
    /// </remarks>
    [HttpPost]
    [ProducesResponseType<MovimentacaoResponse>(StatusCodes.Status201Created)]
    [ProducesResponseType<MovimentacaoResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    [ProducesResponseType(StatusCodes.Status422UnprocessableEntity)]
    public async Task<ActionResult<MovimentacaoResponse>> Registrar(
        CriarMovimentacaoRequest request,
        CancellationToken cancellationToken)
    {
        var (movimentacao, replay) = await servico.RegistrarBaixaAsync(request, cancellationToken);

        return replay
            ? Ok(movimentacao)
            : CreatedAtRoute(nameof(ObterPorReferencia), new { referencia = movimentacao.Referencia }, movimentacao);
    }

    [HttpGet("{referencia}", Name = nameof(ObterPorReferencia))]
    [ProducesResponseType<MovimentacaoResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<MovimentacaoResponse>> ObterPorReferencia(
        string referencia,
        CancellationToken cancellationToken)
    {
        var movimentacao = await servico.BuscarPorReferenciaAsync(referencia, cancellationToken)
            ?? throw new RecursoNaoEncontradoException($"Movimentação '{referencia}' não encontrada.");

        return Ok(movimentacao);
    }
}
