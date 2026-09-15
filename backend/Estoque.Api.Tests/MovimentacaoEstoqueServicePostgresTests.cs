using Estoque.Api.Domain;
using Estoque.Api.Dtos;
using Estoque.Api.Services;
using Microsoft.EntityFrameworkCore;

namespace Estoque.Api.Tests;

/// <summary>
/// Idempotencia contra um PostgreSQL de verdade. Na pista InMemory estes cenarios passam por
/// acidente: aquele provider nao aplica indice unico, entao a garantia nunca e exercitada.
///
/// Cada fase usa o seu proprio contexto. Compartilhar um so faria a assercao ler o rastreador
/// de mudancas em vez do banco, e "foi persistido" passaria mesmo sem nada ter sido gravado.
/// </summary>
[Collection(PostgresCollection.Nome)]
public class MovimentacaoEstoqueServicePostgresTests(PostgresFixture postgres) : IAsyncLifetime
{
    public Task InitializeAsync() => postgres.LimparAsync();

    public Task DisposeAsync() => Task.CompletedTask;

    [Fact]
    public async Task Repetir_a_referencia_debita_uma_vez_so()
    {
        await using (var arranjo = postgres.CriarContexto())
        {
            arranjo.Produtos.Add(new Produto { Codigo = "P001", Descricao = "Teclado", Saldo = 10 });
            await arranjo.SaveChangesAsync();
        }

        await using (var acao = postgres.CriarContexto())
        {
            var servico = new MovimentacaoEstoqueService(acao);

            var pedido = new CriarMovimentacaoRequest
            {
                Referencia = "nota-1",
                Itens = [new MovimentacaoItemRequest { ProdutoCodigo = "P001", Quantidade = 2 }],
            };

            var (_, primeiroReplay) = await servico.RegistrarBaixaAsync(pedido, default);
            var (_, segundoReplay) = await servico.RegistrarBaixaAsync(pedido, default);

            Assert.False(primeiroReplay);
            Assert.True(segundoReplay);
        }

        await using var verificacao = postgres.CriarContexto();

        // 8, nunca 6: a segunda chamada replicou o resultado em vez de debitar de novo.
        Assert.Equal(8, (await verificacao.Produtos.SingleAsync()).Saldo);
        Assert.Equal(1, await verificacao.MovimentacoesEstoque.CountAsync());
    }
}
