using Estoque.Api.Controllers;
using Estoque.Api.Dtos;
using Estoque.Api.Errors;
using Estoque.Api.Services;
using Microsoft.AspNetCore.Mvc;

namespace Estoque.Api.Tests;

public class MovimentacoesEstoqueControllerTests
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
    public async Task Registrar_devolve_201_na_primeira_baixa()
    {
        await using var db = await TestDb.ComProdutosAsync(("P001", 10));
        var controller = new MovimentacoesEstoqueController(new MovimentacaoEstoqueService(db));

        var resultado = await controller.Registrar(Baixa("nota-1", ("P001", 2)), default);

        var criado = Assert.IsType<CreatedAtRouteResult>(resultado.Result);
        Assert.Equal(nameof(MovimentacoesEstoqueController.ObterPorReferencia), criado.RouteName);
        Assert.Equal("nota-1", criado.RouteValues!["referencia"]);

        var movimentacao = Assert.IsType<MovimentacaoResponse>(criado.Value);
        Assert.Equal(8, movimentacao.Itens.Single().SaldoResultante);
    }

    [Fact]
    public async Task Registrar_repetido_devolve_200_em_vez_de_201()
    {
        // 201 cria, 200 replica. O status e o que diz ao chamador que nada foi debitado de novo.
        await using var db = await TestDb.ComProdutosAsync(("P001", 10));
        var controller = new MovimentacoesEstoqueController(new MovimentacaoEstoqueService(db));

        await controller.Registrar(Baixa("nota-1", ("P001", 2)), default);
        var resultado = await controller.Registrar(Baixa("nota-1", ("P001", 2)), default);

        var ok = Assert.IsType<OkObjectResult>(resultado.Result);
        var movimentacao = Assert.IsType<MovimentacaoResponse>(ok.Value);
        Assert.Equal(8, movimentacao.Itens.Single().SaldoResultante);
    }

    [Fact]
    public async Task ObterPorReferencia_devolve_a_movimentacao_gravada()
    {
        await using var db = await TestDb.ComProdutosAsync(("P001", 10));
        var controller = new MovimentacoesEstoqueController(new MovimentacaoEstoqueService(db));
        await controller.Registrar(Baixa("nota-1", ("P001", 2)), default);

        var resultado = await controller.ObterPorReferencia("nota-1", default);

        var movimentacao = Assert.IsType<MovimentacaoResponse>(Assert.IsType<OkObjectResult>(resultado.Result).Value);
        Assert.Equal("nota-1", movimentacao.Referencia);
    }

    [Fact]
    public async Task ObterPorReferencia_inexistente_lanca_RecursoNaoEncontrado()
    {
        await using var db = TestDb.Criar();
        var controller = new MovimentacoesEstoqueController(new MovimentacaoEstoqueService(db));

        var erro = await Assert.ThrowsAsync<RecursoNaoEncontradoException>(
            () => controller.ObterPorReferencia("nao-existe", default));

        Assert.Equal(StatusCodes.Status404NotFound, erro.StatusCode);
        Assert.Contains("nao-existe", erro.Message);
    }

    [Fact]
    public async Task Recusa_do_servico_sobe_do_controller_sem_virar_corpo_de_erro()
    {
        // Convencao do repo: controllers nao montam ProblemDetails; quem faz isso e o handler global.
        await using var db = await TestDb.ComProdutosAsync(("P001", 1));
        var controller = new MovimentacoesEstoqueController(new MovimentacaoEstoqueService(db));

        await Assert.ThrowsAsync<SaldoInsuficienteException>(
            () => controller.Registrar(Baixa("nota-1", ("P001", 99)), default));
    }
}
