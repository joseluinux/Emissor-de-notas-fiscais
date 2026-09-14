using Estoque.Api.Data;
using Estoque.Api.Domain;
using Estoque.Api.Dtos;
using Estoque.Api.Errors;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Estoque.Api.Controllers;

/// <summary>
/// Product registration. Balances are readable here and can be corrected here, but the debit
/// driven by an invoice belongs to <see cref="MovimentacoesEstoqueController"/>.
/// </summary>
[ApiController]
[Route("api/produtos")]
public class ProdutosController(EstoqueDbContext db) : ControllerBase
{
    /// <summary>Lists every product ordered by code.</summary>
    [HttpGet]
    [ProducesResponseType<IEnumerable<ProdutoResponse>>(StatusCodes.Status200OK)]
    public async Task<ActionResult<IEnumerable<ProdutoResponse>>> Listar(CancellationToken cancellationToken)
    {
        // Projected to the DTO inside the query, so EF Core selects only these four columns and
        // the xmin row version never leaves the service.
        var produtos = await db.Produtos
            .OrderBy(p => p.Codigo)
            .Select(p => new ProdutoResponse(p.Id, p.Codigo, p.Descricao, p.Saldo))
            .ToListAsync(cancellationToken);

        return Ok(produtos);
    }

    /// <summary>Returns one product, or 404 if the Id is not registered.</summary>
    [HttpGet("{id:int}", Name = nameof(ObterPorId))]
    [ProducesResponseType<ProdutoResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ProdutoResponse>> ObterPorId(int id, CancellationToken cancellationToken)
    {
        var produto = await db.Produtos
            .Where(p => p.Id == id)
            .Select(p => new ProdutoResponse(p.Id, p.Codigo, p.Descricao, p.Saldo))
            .FirstOrDefaultAsync(cancellationToken);

        // Thrown, not built here: the global handler owns every error body in this API.
        return produto is null
            ? throw new RecursoNaoEncontradoException($"Produto {id} não encontrado.")
            : Ok(produto);
    }

    /// <summary>Registers a product. The code must not already be in use.</summary>
    [HttpPost]
    [ProducesResponseType<ProdutoResponse>(StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<ProdutoResponse>> Criar(
        CriarProdutoRequest request,
        CancellationToken cancellationToken)
    {
        var codigo = request.Codigo.Trim();

        // TODO(revisar): this check is not race-proof — two simultaneous POSTs with the same code
        // both pass it, and the unique index then turns the loser into a 500 instead of the 409
        // above. RegistrarBaixaAsync catches exactly that case on Referencia. Was the race left
        // unhandled here on purpose (registration is not concurrent in practice) or overlooked?
        if (await db.Produtos.AnyAsync(p => p.Codigo == codigo, cancellationToken))
        {
            throw new CodigoDuplicadoException(codigo);
        }

        var produto = new Produto
        {
            Codigo = codigo,
            Descricao = request.Descricao.Trim(),
            Saldo = request.Saldo,
        };

        db.Produtos.Add(produto);
        await db.SaveChangesAsync(cancellationToken);

        var resposta = new ProdutoResponse(produto.Id, produto.Codigo, produto.Descricao, produto.Saldo);

        return CreatedAtRoute(nameof(ObterPorId), new { id = produto.Id }, resposta);
    }

    /// <summary>Updates a product's description and balance.</summary>
    /// <remarks>
    /// Adjusts the registration itself. Debits driven by invoices go through
    /// POST /api/estoque/movimentacoes, which is the audited path. A balance set here overwrites
    /// whatever the debits left and leaves no movement behind.
    /// </remarks>
    [HttpPut("{id:int}")]
    [ProducesResponseType<ProdutoResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<ProdutoResponse>> Atualizar(
        int id,
        AtualizarProdutoRequest request,
        CancellationToken cancellationToken)
    {
        // Tracked query, unlike the reads above: this instance is going to be modified, and its
        // xmin has to travel into the UPDATE for the concurrency check to mean anything.
        var produto = await db.Produtos.FirstOrDefaultAsync(p => p.Id == id, cancellationToken)
            ?? throw new RecursoNaoEncontradoException($"Produto {id} não encontrado.");

        produto.Descricao = request.Descricao.Trim();
        produto.Saldo = request.Saldo;

        try
        {
            await db.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateConcurrencyException)
        {
            // A debit changed this row between the read and the save. Refusing is the point: the
            // caller's Saldo was computed from a balance that no longer exists.
            throw new ConflitoDeConcorrenciaException([produto.Codigo]);
        }

        return Ok(new ProdutoResponse(produto.Id, produto.Codigo, produto.Descricao, produto.Saldo));
    }
}
