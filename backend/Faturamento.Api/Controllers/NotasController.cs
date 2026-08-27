using Faturamento.Api.Data;
using Faturamento.Api.Domain;
using Faturamento.Api.Dtos;
using Faturamento.Api.Infrastructure;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Faturamento.Api.Controllers;

[ApiController]
[Route("api/notas")]
public class NotasController(AppDbContext db, IEstoqueClient estoque) : ControllerBase
{
    /// <summary>Lista as notas com seus itens, em ordem de numeracao.</summary>
    [HttpGet]
    [ProducesResponseType<IReadOnlyList<NotaFiscalResponse>>(StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<NotaFiscalResponse>>> Listar(
        CancellationToken cancellationToken)
    {
        var notas = await db.NotasFiscais
            .AsNoTracking()
            .Include(n => n.Itens)
            .OrderBy(n => n.Numero)
            .ToListAsync(cancellationToken);

        return Ok(notas.Select(NotaFiscalResponse.De).ToList());
    }

    [HttpGet("{id:int}", Name = nameof(ObterPorId))]
    [ProducesResponseType<NotaFiscalResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<NotaFiscalResponse>> ObterPorId(
        int id,
        CancellationToken cancellationToken)
    {
        var nota = await db.NotasFiscais
            .AsNoTracking()
            .Include(n => n.Itens)
            .FirstOrDefaultAsync(n => n.Id == id, cancellationToken);

        if (nota is null)
        {
            return Problem(
                statusCode: StatusCodes.Status404NotFound,
                title: "Nota nao encontrada",
                detail: $"Nao existe nota fiscal com Id {id}.");
        }

        return Ok(NotaFiscalResponse.De(nota));
    }

    /// <summary>Cria uma nota com status Aberta e o proximo Numero da sequence.</summary>
    [HttpPost]
    [ProducesResponseType<NotaFiscalResponse>(StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<NotaFiscalResponse>> Criar(
        CriarNotaFiscalRequest request,
        CancellationToken cancellationToken)
    {
        var nota = new NotaFiscal
        {
            Status = StatusNotaFiscal.Aberta,
            CriadaEm = DateTime.UtcNow,
            Itens = request.Itens
                .Select(i => new NotaFiscalItem
                {
                    ProdutoCodigo = i.ProdutoCodigo,
                    Descricao = i.Descricao,
                    Quantidade = i.Quantidade
                })
                .ToList()
        };

        db.NotasFiscais.Add(nota);

        // Numero fica preenchido aqui: o INSERT usa o default nextval e o Npgsql le o valor de volta.
        await db.SaveChangesAsync(cancellationToken);

        var resposta = NotaFiscalResponse.De(nota);

        return CreatedAtRoute(nameof(ObterPorId), new { id = nota.Id }, resposta);
    }

    /// <summary>Imprime a nota: debita o Estoque e so entao fecha a nota.</summary>
    /// <remarks>
    /// A ordem importa. Debitar primeiro e fechar depois garante que uma falha nunca deixe uma
    /// nota Fechada com o estoque nao debitado. O risco espelhado — debitar e falhar ao fechar —
    /// e coberto pelo Estoque: a baixa e idempotente na referencia "nota-{id}", entao reimprimir
    /// replica a baixa original em vez de debitar de novo.
    /// </remarks>
    [HttpPost("{id:int}/imprimir")]
    [ProducesResponseType<NotaFiscalResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    [ProducesResponseType(StatusCodes.Status422UnprocessableEntity)]
    public async Task<ActionResult<NotaFiscalResponse>> Imprimir(
        int id,
        CancellationToken cancellationToken)
    {
        // Sem AsNoTracking: esta nota vai ser alterada.
        var nota = await db.NotasFiscais
            .Include(n => n.Itens)
            .FirstOrDefaultAsync(n => n.Id == id, cancellationToken);

        if (nota is null)
        {
            return Problem(
                statusCode: StatusCodes.Status404NotFound,
                title: "Nota nao encontrada",
                detail: $"Nao existe nota fiscal com Id {id}.");
        }

        if (nota.Status != StatusNotaFiscal.Aberta)
        {
            throw new NotaNaoAbertaException(nota);
        }

        var baixa = new RegistrarMovimentacaoRequest(
            $"nota-{nota.Id}",
            nota.Itens
                .Select(i => new MovimentacaoItemRequest(i.ProdutoCodigo, i.Quantidade))
                .ToList());

        // Uma unica chamada com todos os itens: o Estoque debita tudo ou nada.
        // Recusas de regra sobem como EstoqueRecusouException e a nota continua Aberta.
        await estoque.RegistrarBaixaAsync(baixa, cancellationToken);

        nota.Status = StatusNotaFiscal.Fechada;
        nota.ImpressaEm = DateTime.UtcNow;

        await db.SaveChangesAsync(cancellationToken);

        return Ok(NotaFiscalResponse.De(nota));
    }
}
