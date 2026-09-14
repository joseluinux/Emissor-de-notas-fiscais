using Estoque.Api.Data;
using Estoque.Api.Domain;
using Estoque.Api.Dtos;
using Estoque.Api.Errors;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace Estoque.Api.Services;

/// <summary>
/// The stock debit, which is the one operation the whole system hinges on. It has to be
/// all-or-nothing across every line, idempotent on the caller's reference, and safe under two
/// simultaneous debits of the same product.
/// </summary>
public sealed class MovimentacaoEstoqueService(EstoqueDbContext db)
{
    /// <summary>PostgreSQL SQLSTATE for unique_violation — see <see cref="EhViolacaoDeUnicidade"/>.</summary>
    private const string ViolacaoDeUnicidade = "23505";

    /// <summary>Reads back a movement by its reference, in the exact shape the debit returns.</summary>
    /// <remarks>
    /// Shared with <see cref="RegistrarBaixaAsync"/> on purpose: a replay must produce the same
    /// body as the original call, and that only holds while both go through one projection.
    /// </remarks>
    public async Task<MovimentacaoResponse?> BuscarPorReferenciaAsync(
        string referencia,
        CancellationToken cancellationToken)
    {
        var chave = referencia.Trim();

        return await db.MovimentacoesEstoque
            .Where(m => m.Referencia == chave)
            .Select(m => new MovimentacaoResponse(
                m.Id,
                m.Referencia,
                m.CriadaEm,
                m.Itens
                    .OrderBy(i => i.ProdutoCodigo)
                    .Select(i => new MovimentacaoItemResponse(i.ProdutoCodigo, i.Quantidade, i.SaldoResultante))
                    .ToList()))
            .FirstOrDefaultAsync(cancellationToken);
    }

    /// <summary>
    /// Debits every line all-or-nothing. Returns the movement plus whether it was a replay of an
    /// earlier call with the same reference rather than a fresh debit.
    /// </summary>
    /// <exception cref="ProdutoDesconhecidoException">At least one code is not registered (422).</exception>
    /// <exception cref="SaldoInsuficienteException">At least one line exceeds the available balance (422).</exception>
    /// <exception cref="ConflitoDeConcorrenciaException">Another debit changed one of these products first (409).</exception>
    public async Task<(MovimentacaoResponse Movimentacao, bool Replay)> RegistrarBaixaAsync(
        CriarMovimentacaoRequest request,
        CancellationToken cancellationToken)
    {
        // The same product may appear on several lines; debit it once with the summed quantity.
        var linhas = request.Itens
            .GroupBy(i => i.ProdutoCodigo.Trim())
            .Select(g => new { Codigo = g.Key, Quantidade = g.Sum(i => i.Quantidade) })
            .ToList();

        // Fast path for a replay: a reference already on file means this debit was applied before,
        // so return the stored result untouched rather than validating and debiting again. The
        // unique index below is what actually enforces this; the lookup only avoids the wasted work.
        var jaRegistrada = await BuscarPorReferenciaAsync(request.Referencia, cancellationToken);
        if (jaRegistrada is not null)
        {
            return (jaRegistrada, true);
        }

        var codigos = linhas.Select(l => l.Codigo).ToList();
        var produtos = await db.Produtos
            .Where(p => codigos.Contains(p.Codigo))
            .ToDictionaryAsync(p => p.Codigo, cancellationToken);

        // Same reasoning as the shortfalls below: name every unknown code in one refusal.
        var desconhecidos = codigos.Where(c => !produtos.ContainsKey(c)).ToList();
        if (desconhecidos.Count > 0)
        {
            throw new ProdutoDesconhecidoException(desconhecidos);
        }

        // Report every shortfall at once — failing on the first would hide the rest from the caller.
        var faltas = linhas
            .Where(l => produtos[l.Codigo].Saldo < l.Quantidade)
            .Select(l => new SaldoInsuficiente(l.Codigo, produtos[l.Codigo].Saldo, l.Quantidade))
            .ToList();
        if (faltas.Count > 0)
        {
            throw new SaldoInsuficienteException(faltas);
        }

        var movimentacao = new MovimentacaoEstoque
        {
            Referencia = request.Referencia.Trim(),
            CriadaEm = DateTime.UtcNow,
        };

        foreach (var linha in linhas)
        {
            var produto = produtos[linha.Codigo];
            produto.Saldo -= linha.Quantidade;

            movimentacao.Itens.Add(new MovimentacaoEstoqueItem
            {
                ProdutoId = produto.Id,
                ProdutoCodigo = produto.Codigo,
                Quantidade = linha.Quantidade,
                SaldoResultante = produto.Saldo,
            });
        }

        db.MovimentacoesEstoque.Add(movimentacao);

        try
        {
            // One SaveChanges: the balance updates and the movement insert share a transaction,
            // so a failure anywhere leaves no partial debit behind.
            await db.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateConcurrencyException ex)
        {
            // xmin changed under us: another debit touched one of these products first. Nothing was
            // written, so the answer is a clean 409 naming the products — deliberately no retry
            // here, the caller decides whether to try again.
            var emConflito = ex.Entries
                .Select(e => e.Entity)
                .OfType<Produto>()
                .Select(p => p.Codigo)
                .ToList();

            throw new ConflitoDeConcorrenciaException(emConflito);
        }
        catch (DbUpdateException ex) when (EhViolacaoDeUnicidade(ex))
        {
            // Two identical calls raced past the check above. The unique index on Referencia is
            // the real idempotency guarantee; the check is only a fast path.
            // Clear the tracker first: this unit of work was rejected whole, and the aborted Saldo
            // changes must not be carried into the read that follows.
            db.ChangeTracker.Clear();

            var replay = await BuscarPorReferenciaAsync(request.Referencia, cancellationToken);

            // No row under this reference means the violation came from some other unique index, so
            // it is not ours to translate into a replay.
            if (replay is null)
            {
                throw;
            }

            return (replay, true);
        }

        // Read back rather than projecting the in-memory graph: Postgres keeps timestamps at
        // microsecond precision, so building the response here would return a CriadaEm that later
        // replays cannot reproduce exactly. Idempotency means the same bytes every time.
        var resposta = await BuscarPorReferenciaAsync(movimentacao.Referencia, cancellationToken)
            ?? throw new InvalidOperationException(
                $"Movimentação '{movimentacao.Referencia}' não encontrada logo após ser gravada.");

        return (resposta, false);
    }

    private static bool EhViolacaoDeUnicidade(DbUpdateException exception) =>
        exception.InnerException is PostgresException { SqlState: ViolacaoDeUnicidade };
}
