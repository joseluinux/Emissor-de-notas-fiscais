using Estoque.Api.Domain;
using Estoque.Api.Dtos;
using Estoque.Api.Errors;
using Estoque.Api.Services;
using Microsoft.EntityFrameworkCore;

namespace Estoque.Api.Tests;

/// <summary>
/// Concorrencia e atomicidade contra um PostgreSQL de verdade. Nenhum destes cenarios existe na
/// pista InMemory: la nao ha xmin para gerar conflito nem transacao para reverter.
/// </summary>
[Collection(PostgresCollection.Nome)]
public class ConcorrenciaPostgresTests(PostgresFixture postgres) : IAsyncLifetime
{
    public Task InitializeAsync() => postgres.LimparAsync();

    public Task DisposeAsync() => Task.CompletedTask;

    private static CriarMovimentacaoRequest Baixa(string referencia, params (string Codigo, int Quantidade)[] itens)
        => new()
        {
            Referencia = referencia,
            Itens = [.. itens.Select(i => new MovimentacaoItemRequest
            {
                ProdutoCodigo = i.Codigo,
                Quantidade = i.Quantidade,
            })],
        };

    /// <summary>
    /// Executa uma baixa e devolve a excecao, ou null se venceu. Cada chamada abre o SEU proprio
    /// contexto: DbContext nao e thread-safe, e duas tarefas compartilhando um explodiriam por um
    /// motivo que nada tem a ver com o que este teste quer medir.
    /// </summary>
    private async Task<Exception?> DebitarAsync(string referencia, int quantidade)
    {
        await using var db = postgres.CriarContexto();
        var servico = new MovimentacaoEstoqueService(db);

        try
        {
            await servico.RegistrarBaixaAsync(Baixa(referencia, ("P001", quantidade)), default);
            return null;
        }
        catch (Exception e)
        {
            return e;
        }
    }

    private async Task SemearAsync(int saldo)
    {
        await using var arranjo = postgres.CriarContexto();
        arranjo.Produtos.Add(new Produto { Codigo = "P001", Descricao = "Item raro", Saldo = saldo });
        await arranjo.SaveChangesAsync();
    }

    [Fact]
    public async Task Duas_baixas_simultaneas_sobre_saldo_1_deixam_so_uma_passar()
    {
        // O cenario do requisito opcional (a): um produto com saldo 1 disputado por duas notas.
        await SemearAsync(saldo: 1);

        // Referencias DIFERENTES: sao duas operacoes distintas competindo pelo mesmo saldo, nao a
        // mesma operacao repetida. Quem arbitra aqui e o xmin, nao o indice unico.
        var resultados = await Task.WhenAll(
            DebitarAsync("nota-1", 1),
            DebitarAsync("nota-2", 1));

        Assert.Equal(1, resultados.Count(r => r is null));

        // Dois desfechos sao validos para a perdedora, e os dois protegem o saldo: perdeu a corrida
        // do xmin (409) ou leu depois do commit da vencedora e viu saldo zero (422). Aceitar so um
        // deles tornaria o teste instavel sem tornar o sistema mais correto.
        var perdedora = Assert.Single(resultados, r => r is not null)!;
        Assert.True(
            perdedora is ConflitoDeConcorrenciaException or SaldoInsuficienteException,
            $"esperava conflito de concorrencia ou saldo insuficiente, veio "
                + $"{perdedora.GetType().Name}: {perdedora.Message}");

        await using var verificacao = postgres.CriarContexto();

        // O que realmente importa: o saldo nunca fica negativo.
        Assert.Equal(0, (await verificacao.Produtos.SingleAsync()).Saldo);
        Assert.Equal(1, await verificacao.MovimentacoesEstoque.CountAsync());
    }

    [Fact]
    public async Task Duas_baixas_simultaneas_com_a_mesma_referencia_debitam_uma_vez_so()
    {
        await SemearAsync(saldo: 10);

        // Referencia IGUAL: e a mesma operacao chegando duas vezes, nao duas operacoes disputando
        // o saldo. As duas passam pela consulta do fast-path sem ver uma a outra, entao quem
        // arbitra aqui e o indice unico — o Postgres rejeita a segunda gravacao com 23505 e o
        // servico traduz a rejeicao em replay. Este e o unico teste que alcanca aquele catch.
        var resultados = await Task.WhenAll(
            DebitarAsync("nota-1", 2),
            DebitarAsync("nota-1", 2));

        // As DUAS vencem: replay nao e erro. Contraste com o teste acima, onde uma perde.
        Assert.Equal(2, resultados.Count(r => r is null));

        await using var verificacao = postgres.CriarContexto();
        Assert.Equal(8, (await verificacao.Produtos.SingleAsync()).Saldo);
        Assert.Equal(1, await verificacao.MovimentacoesEstoque.CountAsync());
    }
}
