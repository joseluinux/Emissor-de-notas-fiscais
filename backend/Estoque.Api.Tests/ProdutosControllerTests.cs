using Estoque.Api.Controllers;
using Estoque.Api.Domain;
using Estoque.Api.Dtos;
using Estoque.Api.Errors;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Estoque.Api.Tests;

public class ProdutosControllerTests
{
    [Fact]
    public async Task Listar_devolve_os_produtos_ordenados_por_codigo()
    {
        await using var db = await TestDb.ComProdutosAsync(("P003", 1), ("P001", 5), ("P002", 3));
        var controller = new ProdutosController(db);

        var resultado = await controller.Listar(default);

        var produtos = Assert.IsType<List<ProdutoResponse>>(Assert.IsType<OkObjectResult>(resultado.Result).Value);
        Assert.Equal(["P001", "P002", "P003"], produtos.Select(p => p.Codigo));
    }

    [Fact]
    public async Task Listar_sem_produtos_devolve_lista_vazia()
    {
        await using var db = TestDb.Criar();
        var controller = new ProdutosController(db);

        var resultado = await controller.Listar(default);

        var produtos = Assert.IsType<List<ProdutoResponse>>(Assert.IsType<OkObjectResult>(resultado.Result).Value);
        Assert.Empty(produtos);
    }

    [Fact]
    public async Task ObterPorId_devolve_o_produto_quando_existe()
    {
        await using var db = await TestDb.ComProdutosAsync(("P001", 7));
        var id = (await db.Produtos.SingleAsync()).Id;
        var controller = new ProdutosController(db);

        var resultado = await controller.ObterPorId(id, default);

        var produto = Assert.IsType<ProdutoResponse>(Assert.IsType<OkObjectResult>(resultado.Result).Value);
        Assert.Equal("P001", produto.Codigo);
        Assert.Equal(7, produto.Saldo);
    }

    [Fact]
    public async Task ObterPorId_inexistente_lanca_RecursoNaoEncontrado()
    {
        await using var db = TestDb.Criar();
        var controller = new ProdutosController(db);

        var erro = await Assert.ThrowsAsync<RecursoNaoEncontradoException>(
            () => controller.ObterPorId(999, default));

        Assert.Equal(StatusCodes.Status404NotFound, erro.StatusCode);
        Assert.Contains("999", erro.Message);
    }

    [Fact]
    public async Task Criar_grava_o_produto_e_devolve_201()
    {
        await using var db = TestDb.Criar();
        var controller = new ProdutosController(db);

        var resultado = await controller.Criar(
            new CriarProdutoRequest { Codigo = "P001", Descricao = "Teclado", Saldo = 10 },
            default);

        var criado = Assert.IsType<CreatedAtRouteResult>(resultado.Result);
        var produto = Assert.IsType<ProdutoResponse>(criado.Value);
        Assert.Equal("P001", produto.Codigo);
        Assert.Equal(10, produto.Saldo);
        Assert.Equal(nameof(ProdutosController.ObterPorId), criado.RouteName);

        Assert.Equal(1, await db.Produtos.CountAsync());
    }

    [Fact]
    public async Task Criar_remove_espacos_em_volta_do_codigo_e_da_descricao()
    {
        await using var db = TestDb.Criar();
        var controller = new ProdutosController(db);

        await controller.Criar(
            new CriarProdutoRequest { Codigo = "  P001  ", Descricao = "  Teclado  ", Saldo = 1 },
            default);

        var produto = await db.Produtos.SingleAsync();
        Assert.Equal("P001", produto.Codigo);
        Assert.Equal("Teclado", produto.Descricao);
    }

    [Fact]
    public async Task Criar_com_codigo_repetido_lanca_CodigoDuplicado_e_nao_grava()
    {
        await using var db = await TestDb.ComProdutosAsync(("P001", 5));
        var controller = new ProdutosController(db);

        var erro = await Assert.ThrowsAsync<CodigoDuplicadoException>(
            () => controller.Criar(
                new CriarProdutoRequest { Codigo = "P001", Descricao = "Outro", Saldo = 1 },
                default));

        Assert.Equal(StatusCodes.Status409Conflict, erro.StatusCode);
        Assert.Equal(1, await db.Produtos.CountAsync());
    }

    [Fact]
    public async Task Criar_compara_o_codigo_ja_aparado_ao_detectar_duplicidade()
    {
        await using var db = await TestDb.ComProdutosAsync(("P001", 5));
        var controller = new ProdutosController(db);

        await Assert.ThrowsAsync<CodigoDuplicadoException>(
            () => controller.Criar(
                new CriarProdutoRequest { Codigo = "  P001  ", Descricao = "Outro", Saldo = 1 },
                default));
    }

    [Fact]
    public async Task Atualizar_altera_descricao_e_saldo()
    {
        await using var db = await TestDb.ComProdutosAsync(("P001", 5));
        var id = (await db.Produtos.SingleAsync()).Id;
        var controller = new ProdutosController(db);

        var resultado = await controller.Atualizar(
            id,
            new AtualizarProdutoRequest { Descricao = "  Teclado mecanico  ", Saldo = 42 },
            default);

        var produto = Assert.IsType<ProdutoResponse>(Assert.IsType<OkObjectResult>(resultado.Result).Value);
        Assert.Equal("Teclado mecanico", produto.Descricao);
        Assert.Equal(42, produto.Saldo);

        var gravado = await db.Produtos.SingleAsync();
        Assert.Equal(42, gravado.Saldo);
        // O codigo e chave de negocio: atualizar a nota nunca o altera.
        Assert.Equal("P001", gravado.Codigo);
    }

    [Fact]
    public async Task Atualizar_inexistente_lanca_RecursoNaoEncontrado()
    {
        await using var db = TestDb.Criar();
        var controller = new ProdutosController(db);

        await Assert.ThrowsAsync<RecursoNaoEncontradoException>(
            () => controller.Atualizar(
                999,
                new AtualizarProdutoRequest { Descricao = "Nada", Saldo = 0 },
                default));
    }
}
