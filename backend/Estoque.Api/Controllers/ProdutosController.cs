using Estoque.Api.Data;
using Estoque.Api.Domain;
using Estoque.Api.Dtos;
using Estoque.Api.Errors;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Estoque.Api.Controllers;

[ApiController]
[Route("api/produtos")]
public class ProdutosController(EstoqueDbContext db) : ControllerBase
{
    [HttpGet]
    [ProducesResponseType<IEnumerable<ProdutoResponse>>(StatusCodes.Status200OK)]
    public async Task<ActionResult<IEnumerable<ProdutoResponse>>> Listar(CancellationToken cancellationToken)
    {
        var produtos = await db.Produtos
            .OrderBy(p => p.Codigo)
            .Select(p => new ProdutoResponse(p.Id, p.Codigo, p.Descricao, p.Saldo))
            .ToListAsync(cancellationToken);

        return Ok(produtos);
    }

    [HttpGet("{id:int}", Name = nameof(ObterPorId))]
    [ProducesResponseType<ProdutoResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ProdutoResponse>> ObterPorId(int id, CancellationToken cancellationToken)
    {
        var produto = await db.Produtos
            .Where(p => p.Id == id)
            .Select(p => new ProdutoResponse(p.Id, p.Codigo, p.Descricao, p.Saldo))
            .FirstOrDefaultAsync(cancellationToken);

        return produto is null
            ? throw new RecursoNaoEncontradoException($"Produto {id} não encontrado.")
            : Ok(produto);
    }

    [HttpPost]
    [ProducesResponseType<ProdutoResponse>(StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<ProdutoResponse>> Criar(
        CriarProdutoRequest request,
        CancellationToken cancellationToken)
    {
        var codigo = request.Codigo.Trim();

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

    /// <remarks>
    /// Adjusts the registration itself. Debits driven by invoices go through
    /// POST /api/estoque/movimentacoes, which is the audited path.
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
            throw new ConflitoDeConcorrenciaException([produto.Codigo]);
        }

        return Ok(new ProdutoResponse(produto.Id, produto.Codigo, produto.Descricao, produto.Saldo));
    }
}
