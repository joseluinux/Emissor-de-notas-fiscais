using Faturamento.Api.Data;
using Faturamento.Api.Domain;
using Faturamento.Api.Dtos;
using Faturamento.Api.Infrastructure;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Faturamento.Api.Controllers;

/// <summary>
/// Invoices: creation, reading, and the print flow that debits Estoque across the network.
/// </summary>
[ApiController]
[Route("api/notas")]
public class NotasController(AppDbContext db, IEstoqueClient estoque) : ControllerBase
{
    /// <summary>Lists the invoices with their items, in numbering order.</summary>
    [HttpGet]
    [ProducesResponseType<IReadOnlyList<NotaFiscalResponse>>(StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<NotaFiscalResponse>>> Listar(
        CancellationToken cancellationToken)
    {
        // AsNoTracking on the read paths: nothing here is going to be modified, so the change
        // tracker would only cost memory. Include because the items are part of the response.
        var notas = await db.NotasFiscais
            .AsNoTracking()
            .Include(n => n.Itens)
            .OrderBy(n => n.Numero)
            .ToListAsync(cancellationToken);

        return Ok(notas.Select(NotaFiscalResponse.De).ToList());
    }

    /// <summary>Returns one invoice with its items, or 404 if the Id does not exist.</summary>
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
            throw new NotaNaoEncontradaException(id);
        }

        return Ok(NotaFiscalResponse.De(nota));
    }

    /// <summary>Creates an invoice with status Aberta and the next Numero from the sequence.</summary>
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

        // Numero is filled in here: the INSERT uses the nextval default and Npgsql reads the value
        // back. Creation does not touch Estoque — no balance is reserved until the invoice is printed.
        await db.SaveChangesAsync(cancellationToken);

        var resposta = NotaFiscalResponse.De(nota);

        return CreatedAtRoute(nameof(ObterPorId), new { id = nota.Id }, resposta);
    }

    /// <summary>Prints the invoice: debits Estoque and only then closes the invoice.</summary>
    /// <remarks>
    /// The order matters. Debiting first and closing afterwards guarantees that a failure never
    /// leaves an invoice Fechada with stock un-debited. The mirrored risk — debiting and then
    /// failing to close — is covered by Estoque: the debit is idempotent on the reference
    /// "nota-{id}", so reprinting replays the original debit instead of debiting again.
    /// <para>
    /// Known limit: if Estoque is unreachable the failure surfaces as a 500, not the friendly 503
    /// the brief asks for. That is mandatory requirement 2, deliberately deferred — see NOTES.md.
    /// </para>
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
        // No AsNoTracking: this invoice is going to be modified.
        var nota = await db.NotasFiscais
            .Include(n => n.Itens)
            .FirstOrDefaultAsync(n => n.Id == id, cancellationToken);

        if (nota is null)
        {
            throw new NotaNaoEncontradaException(id);
        }

        // TODO(revisar): this check is not protected against a race — NotaFiscal has no concurrency
        // token, unlike Produto with its xmin, so two simultaneous prints of the same invoice both
        // pass here. Stock is safe either way (one reference, one debit), and both requests would
        // close the invoice to the same state, so was leaving it unguarded a considered call?
        if (nota.Status != StatusNotaFiscal.Aberta)
        {
            throw new NotaNaoAbertaException(nota);
        }

        // The invoice Id is the idempotency key on the Estoque side: the same invoice always
        // produces the same reference, which is what makes a repeated print replay rather than debit.
        var baixa = new RegistrarMovimentacaoRequest(
            $"nota-{nota.Id}",
            nota.Itens
                .Select(i => new MovimentacaoItemRequest(i.ProdutoCodigo, i.Quantidade))
                .ToList());

        // A single call with every item: Estoque debits all or nothing. Rule refusals come back as
        // EstoqueRecusouException and the invoice stays Aberta, because the lines below never run.
        await estoque.RegistrarBaixaAsync(baixa, cancellationToken);

        nota.Status = StatusNotaFiscal.Fechada;
        nota.ImpressaEm = DateTime.UtcNow;

        await db.SaveChangesAsync(cancellationToken);

        return Ok(NotaFiscalResponse.De(nota));
    }
}
