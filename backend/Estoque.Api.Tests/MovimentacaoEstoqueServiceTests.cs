using Estoque.Api.Dtos;
using Estoque.Api.Errors;
using Estoque.Api.Services;
using Microsoft.EntityFrameworkCore;

namespace Estoque.Api.Tests;

/// <summary>
/// O endpoint mais importante do sistema: debita tudo ou nada e e idempotente na referencia.
/// </summary>
public class MovimentacaoEstoqueServiceTests
{
    private static CriarMovimentacaoRequest Baixa(string referencia, params (string Codigo, int Quantidade)[] itens)
        => new()
        {
            Referencia = referencia,
            Itens = itens
                .Select(i => new MovimentacaoItemRequest { ProdutoCodigo = i.Codigo, Quantidade = i.Quantidade })
                .ToList(),
        };

    [Fact]
    public async Task Debita_o_saldo_do_produto()
    {
        // O exemplo do brief: saldo 10, nota usa 2, saldo vira 8.
        await using var db = await TestDb.ComProdutosAsync(("P001", 10));
        var servico = new MovimentacaoEstoqueService(db);

        var (movimentacao, replay) = await servico.RegistrarBaixaAsync(Baixa("nota-1", ("P001", 2)), default);

        Assert.False(replay);
        Assert.Equal(8, (await db.Produtos.SingleAsync()).Saldo);

        var item = Assert.Single(movimentacao.Itens);
        Assert.Equal("P001", item.ProdutoCodigo);
        Assert.Equal(2, item.Quantidade);
        Assert.Equal(8, item.SaldoResultante);
    }

    [Fact]
    public async Task Debita_varios_produtos_na_mesma_movimentacao()
    {
        await using var db = await TestDb.ComProdutosAsync(("P001", 10), ("P002", 5));
        var servico = new MovimentacaoEstoqueService(db);

        var (movimentacao, _) = await servico.RegistrarBaixaAsync(
            Baixa("nota-1", ("P001", 2), ("P002", 5)),
            default);

        Assert.Equal(2, movimentacao.Itens.Count);
        var saldos = await db.Produtos.ToDictionaryAsync(p => p.Codigo, p => p.Saldo);
        Assert.Equal(8, saldos["P001"]);
        Assert.Equal(0, saldos["P002"]);
    }

    [Fact]
    public async Task Soma_as_quantidades_quando_o_mesmo_produto_aparece_em_varias_linhas()
    {
        await using var db = await TestDb.ComProdutosAsync(("P001", 10));
        var servico = new MovimentacaoEstoqueService(db);

        var (movimentacao, _) = await servico.RegistrarBaixaAsync(
            Baixa("nota-1", ("P001", 2), ("P001", 3)),
            default);

        // Uma linha so, com a soma — debitar duas vezes daria o mesmo saldo mas um extrato errado.
        var item = Assert.Single(movimentacao.Itens);
        Assert.Equal(5, item.Quantidade);
        Assert.Equal(5, item.SaldoResultante);
        Assert.Equal(5, (await db.Produtos.SingleAsync()).Saldo);
    }

    [Fact]
    public async Task Produto_desconhecido_recusa_a_baixa_inteira()
    {
        await using var db = await TestDb.ComProdutosAsync(("P001", 10));
        var servico = new MovimentacaoEstoqueService(db);

        var erro = await Assert.ThrowsAsync<ProdutoDesconhecidoException>(
            () => servico.RegistrarBaixaAsync(Baixa("nota-1", ("P001", 1), ("P404", 1)), default));

        Assert.Contains("P404", erro.Message);
        Assert.Equal(StatusCodes.Status422UnprocessableEntity, erro.StatusCode);

        // Tudo ou nada: o produto valido tambem nao foi debitado.
        Assert.Equal(10, (await db.Produtos.SingleAsync()).Saldo);
        Assert.Empty(await db.MovimentacoesEstoque.ToListAsync());
    }

    [Fact]
    public async Task Saldo_insuficiente_recusa_a_baixa_e_nomeia_o_produto()
    {
        await using var db = await TestDb.ComProdutosAsync(("P001", 1));
        var servico = new MovimentacaoEstoqueService(db);

        var erro = await Assert.ThrowsAsync<SaldoInsuficienteException>(
            () => servico.RegistrarBaixaAsync(Baixa("nota-1", ("P001", 999)), default));

        Assert.Contains("P001", erro.Message);
        Assert.Equal(StatusCodes.Status422UnprocessableEntity, erro.StatusCode);
        Assert.Equal(1, (await db.Produtos.SingleAsync()).Saldo);
    }

    [Fact]
    public async Task Saldo_insuficiente_reporta_todas_as_faltas_de_uma_vez()
    {
        await using var db = await TestDb.ComProdutosAsync(("P001", 1), ("P002", 0), ("P003", 50));
        var servico = new MovimentacaoEstoqueService(db);

        var erro = await Assert.ThrowsAsync<SaldoInsuficienteException>(
            () => servico.RegistrarBaixaAsync(
                Baixa("nota-1", ("P001", 5), ("P002", 5), ("P003", 5)),
                default));

        // Parar na primeira falta esconderia as demais de quem chamou.
        Assert.Contains("P001", erro.Message);
        Assert.Contains("P002", erro.Message);
        Assert.DoesNotContain("P003", erro.Message);

        var faltas = Assert.IsAssignableFrom<IReadOnlyCollection<SaldoInsuficiente>>(erro.Extensions["faltas"]);
        Assert.Equal(2, faltas.Count);
    }

    [Fact]
    public async Task Baixa_exata_do_saldo_e_permitida()
    {
        await using var db = await TestDb.ComProdutosAsync(("P001", 3));
        var servico = new MovimentacaoEstoqueService(db);

        await servico.RegistrarBaixaAsync(Baixa("nota-1", ("P001", 3)), default);

        Assert.Equal(0, (await db.Produtos.SingleAsync()).Saldo);
    }

    [Fact]
    public async Task Repetir_a_referencia_replica_o_resultado_sem_debitar_de_novo()
    {
        // Requisito opcional (c): operacoes repetidas nao podem ter efeito colateral extra.
        await using var db = await TestDb.ComProdutosAsync(("P001", 10));
        var servico = new MovimentacaoEstoqueService(db);

        var (primeira, primeiroReplay) = await servico.RegistrarBaixaAsync(Baixa("nota-1", ("P001", 2)), default);
        var (segunda, segundoReplay) = await servico.RegistrarBaixaAsync(Baixa("nota-1", ("P001", 2)), default);

        Assert.False(primeiroReplay);
        Assert.True(segundoReplay);

        // Mesmos bytes, inclusive o CriadaEm original.
        Assert.Equal(primeira.Id, segunda.Id);
        Assert.Equal(primeira.CriadaEm, segunda.CriadaEm);
        Assert.Equal(primeira.Itens.Single().SaldoResultante, segunda.Itens.Single().SaldoResultante);

        Assert.Equal(8, (await db.Produtos.SingleAsync()).Saldo);
        Assert.Single(await db.MovimentacoesEstoque.ToListAsync());
    }

    [Fact]
    public async Task Replay_ignora_os_itens_enviados_na_repeticao()
    {
        await using var db = await TestDb.ComProdutosAsync(("P001", 10));
        var servico = new MovimentacaoEstoqueService(db);

        await servico.RegistrarBaixaAsync(Baixa("nota-1", ("P001", 2)), default);
        var (segunda, replay) = await servico.RegistrarBaixaAsync(Baixa("nota-1", ("P001", 7)), default);

        // A referencia manda: o corpo diferente nao debita os 7.
        Assert.True(replay);
        Assert.Equal(2, segunda.Itens.Single().Quantidade);
        Assert.Equal(8, (await db.Produtos.SingleAsync()).Saldo);
    }

    [Fact]
    public async Task Referencias_diferentes_debitam_cada_uma()
    {
        await using var db = await TestDb.ComProdutosAsync(("P001", 10));
        var servico = new MovimentacaoEstoqueService(db);

        await servico.RegistrarBaixaAsync(Baixa("nota-1", ("P001", 2)), default);
        await servico.RegistrarBaixaAsync(Baixa("nota-2", ("P001", 3)), default);

        Assert.Equal(5, (await db.Produtos.SingleAsync()).Saldo);
        Assert.Equal(2, await db.MovimentacoesEstoque.CountAsync());
    }

    [Fact]
    public async Task Referencia_e_codigo_sao_aparados()
    {
        await using var db = await TestDb.ComProdutosAsync(("P001", 10));
        var servico = new MovimentacaoEstoqueService(db);

        var (movimentacao, _) = await servico.RegistrarBaixaAsync(
            Baixa("  nota-1  ", ("  P001  ", 2)),
            default);

        Assert.Equal("nota-1", movimentacao.Referencia);

        // E o replay reconhece a mesma referencia sem os espacos.
        var (_, replay) = await servico.RegistrarBaixaAsync(Baixa("nota-1", ("P001", 2)), default);
        Assert.True(replay);
    }

    [Fact]
    public async Task BuscarPorReferencia_devolve_null_quando_nao_existe()
    {
        await using var db = TestDb.Criar();
        var servico = new MovimentacaoEstoqueService(db);

        Assert.Null(await servico.BuscarPorReferenciaAsync("nao-existe", default));
    }

    [Fact]
    public async Task BuscarPorReferencia_ordena_os_itens_por_codigo()
    {
        await using var db = await TestDb.ComProdutosAsync(("P001", 10), ("P002", 10), ("P003", 10));
        var servico = new MovimentacaoEstoqueService(db);

        await servico.RegistrarBaixaAsync(Baixa("nota-1", ("P003", 1), ("P001", 1), ("P002", 1)), default);

        var movimentacao = await servico.BuscarPorReferenciaAsync("nota-1", default);

        Assert.NotNull(movimentacao);
        Assert.Equal(["P001", "P002", "P003"], movimentacao.Itens.Select(i => i.ProdutoCodigo));
    }

    [Fact]
    public async Task Grava_o_saldo_resultante_de_cada_item()
    {
        await using var db = await TestDb.ComProdutosAsync(("P001", 10), ("P002", 4));
        var servico = new MovimentacaoEstoqueService(db);

        var (movimentacao, _) = await servico.RegistrarBaixaAsync(
            Baixa("nota-1", ("P001", 3), ("P002", 4)),
            default);

        // O extrato guarda o saldo apos a baixa, que e o que aparece na auditoria da demo.
        var porCodigo = movimentacao.Itens.ToDictionary(i => i.ProdutoCodigo, i => i.SaldoResultante);
        Assert.Equal(7, porCodigo["P001"]);
        Assert.Equal(0, porCodigo["P002"]);
    }
}
