using Estoque.Api.Domain;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace Estoque.Api.Tests;

/// <summary>
/// Prova que a pista de PostgreSQL real funciona — e que ela pega o que o InMemory nao pega.
/// </summary>
[Collection(PostgresCollection.Nome)]
public class PostgresFixtureSmokeTests(PostgresFixture postgres) : IAsyncLifetime
{
    public Task InitializeAsync() => postgres.LimparAsync();

    public Task DisposeAsync() => Task.CompletedTask;

    [Fact]
    public async Task O_indice_unico_de_Referencia_rejeita_a_duplicata()
    {
        // Exatamente a operacao que o InMemory aceitou sem reclamar.
        await using (var primeiro = postgres.CriarContexto())
        {
            primeiro.MovimentacoesEstoque.Add(
                new MovimentacaoEstoque { Referencia = "nota-1", CriadaEm = DateTime.UtcNow });
            await primeiro.SaveChangesAsync();
        }

        await using var segundo = postgres.CriarContexto();
        segundo.MovimentacoesEstoque.Add(
            new MovimentacaoEstoque { Referencia = "nota-1", CriadaEm = DateTime.UtcNow });

        var erro = await Assert.ThrowsAsync<DbUpdateException>(() => segundo.SaveChangesAsync());

        // 23505 = unique_violation. E o codigo que o servico traduz em replay.
        Assert.Equal("23505", (erro.InnerException as PostgresException)?.SqlState);
    }

    [Fact]
    public async Task Um_contexto_novo_nao_enxerga_o_que_nao_foi_gravado()
    {
        await using (var arranjo = postgres.CriarContexto())
        {
            arranjo.Produtos.Add(new Produto { Codigo = "P001", Descricao = "Teclado", Saldo = 10 });
            await arranjo.SaveChangesAsync();

            (await arranjo.Produtos.SingleAsync()).Saldo = 8;   // muta, NAO salva
        }

        await using var verificacao = postgres.CriarContexto();
        Assert.Equal(10, (await verificacao.Produtos.SingleAsync()).Saldo);
    }
}
