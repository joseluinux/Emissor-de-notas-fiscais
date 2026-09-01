using System.Runtime.CompilerServices;
using Estoque.Api.Data;
using Estoque.Api.Domain;
using Microsoft.EntityFrameworkCore;

namespace Estoque.Api.Tests;

/// <summary>
/// Banco InMemory descartavel, um por teste, para que a ordem de execucao nunca importe.
///
/// Limites conhecidos do provider — cobertos ao vivo contra o Postgres, nao aqui:
/// indices unicos nao sao aplicados, entao a corrida que cai na violacao 23505 nao acontece;
/// e o xmin de <see cref="Produto.Version"/> nao existe, entao nao ha
/// DbUpdateConcurrencyException natural para disparar.
/// </summary>
internal static class TestDb
{
    public static EstoqueDbContext Criar([CallerMemberName] string nome = "")
        => new(new DbContextOptionsBuilder<EstoqueDbContext>()
            .UseInMemoryDatabase($"{nome}-{Guid.NewGuid()}")
            .Options);

    /// <summary>Cria o contexto ja com os produtos informados gravados.</summary>
    public static async Task<EstoqueDbContext> ComProdutosAsync(
        params (string Codigo, int Saldo)[] produtos)
    {
        var db = Criar($"seed-{Guid.NewGuid()}");

        db.Produtos.AddRange(produtos.Select(p => new Produto
        {
            Codigo = p.Codigo,
            Descricao = $"Produto {p.Codigo}",
            Saldo = p.Saldo,
        }));

        await db.SaveChangesAsync();

        return db;
    }
}
