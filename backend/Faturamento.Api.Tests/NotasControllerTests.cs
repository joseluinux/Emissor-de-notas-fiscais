using Faturamento.Api.Controllers;
using Faturamento.Api.Data;
using Faturamento.Api.Domain;
using Faturamento.Api.Dtos;
using Faturamento.Api.Infrastructure;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Faturamento.Api.Tests;

public class NotasControllerTests
{
    /// <summary>Estoque de mentira: registra o que foi pedido e responde o que o teste mandar.</summary>
    private sealed class EstoqueFalso(Func<RegistrarMovimentacaoRequest, MovimentacaoEstoqueResponse>? resposta = null)
        : IEstoqueClient
    {
        public RegistrarMovimentacaoRequest? Recebido { get; private set; }

        public int Chamadas { get; private set; }

        public Exception? Erro { get; init; }

        public Task<MovimentacaoEstoqueResponse> RegistrarBaixaAsync(
            RegistrarMovimentacaoRequest request,
            CancellationToken cancellationToken)
        {
            Recebido = request;
            Chamadas++;

            if (Erro is not null)
            {
                throw Erro;
            }

            var padrao = new MovimentacaoEstoqueResponse(1, request.Referencia, DateTime.UtcNow, []);

            return Task.FromResult(resposta?.Invoke(request) ?? padrao);
        }
    }

    private static async Task<AppDbContext> ComNotasAsync(params NotaFiscal[] notas)
    {
        var db = TestDb.Criar($"notas-{Guid.NewGuid()}");
        db.NotasFiscais.AddRange(notas);
        await db.SaveChangesAsync();

        return db;
    }

    [Fact]
    public async Task Listar_devolve_as_notas_ordenadas_por_numero_com_os_itens()
    {
        await using var db = await ComNotasAsync(
            TestDb.Nota(3, StatusNotaFiscal.Aberta, ("P003", 1)),
            TestDb.Nota(1, StatusNotaFiscal.Aberta, ("P001", 1), ("P002", 2)),
            TestDb.Nota(2, StatusNotaFiscal.Fechada, ("P002", 1)));
        var controller = new NotasController(db, new EstoqueFalso());

        var resultado = await controller.Listar(default);

        var notas = Assert.IsType<List<NotaFiscalResponse>>(Assert.IsType<OkObjectResult>(resultado.Result).Value);
        Assert.Equal([1, 2, 3], notas.Select(n => n.Numero));
        Assert.Equal(2, notas[0].Itens.Count);
    }

    [Fact]
    public async Task Listar_sem_notas_devolve_lista_vazia()
    {
        await using var db = TestDb.Criar();
        var controller = new NotasController(db, new EstoqueFalso());

        var resultado = await controller.Listar(default);

        Assert.Empty(Assert.IsType<List<NotaFiscalResponse>>(Assert.IsType<OkObjectResult>(resultado.Result).Value));
    }

    [Fact]
    public async Task ObterPorId_devolve_a_nota_com_os_itens()
    {
        await using var db = await ComNotasAsync(TestDb.Nota(1, StatusNotaFiscal.Aberta, ("P001", 2)));
        var id = (await db.NotasFiscais.SingleAsync()).Id;
        var controller = new NotasController(db, new EstoqueFalso());

        var resultado = await controller.ObterPorId(id, default);

        var nota = Assert.IsType<NotaFiscalResponse>(Assert.IsType<OkObjectResult>(resultado.Result).Value);
        Assert.Equal("P001", nota.Itens.Single().ProdutoCodigo);
    }

    [Fact]
    public async Task ObterPorId_inexistente_devolve_404_como_ProblemDetails()
    {
        await using var db = TestDb.Criar();
        var controller = new NotasController(db, new EstoqueFalso());

        var resultado = await controller.ObterPorId(999, default);

        var problema = Assert.IsType<ProblemDetails>(Assert.IsType<ObjectResult>(resultado.Result).Value);
        Assert.Equal(StatusCodes.Status404NotFound, problema.Status);
        Assert.Contains("999", problema.Detail);
    }

    [Fact]
    public async Task Criar_grava_a_nota_como_Aberta_com_os_itens()
    {
        await using var db = TestDb.Criar();
        var controller = new NotasController(db, new EstoqueFalso());

        var resultado = await controller.Criar(
            new CriarNotaFiscalRequest
            {
                Itens =
                [
                    new CriarNotaFiscalItemRequest { ProdutoCodigo = "P001", Descricao = "Teclado", Quantidade = 2 },
                    new CriarNotaFiscalItemRequest { ProdutoCodigo = "P002", Descricao = "Mouse", Quantidade = 3 },
                ],
            },
            default);

        var criada = Assert.IsType<CreatedAtRouteResult>(resultado.Result);
        var nota = Assert.IsType<NotaFiscalResponse>(criada.Value);
        Assert.Equal("Aberta", nota.Status);
        Assert.Null(nota.ImpressaEm);
        Assert.Equal(2, nota.Itens.Count);
        Assert.Equal(nameof(NotasController.ObterPorId), criada.RouteName);

        var gravada = await db.NotasFiscais.Include(n => n.Itens).SingleAsync();
        Assert.Equal(StatusNotaFiscal.Aberta, gravada.Status);
        Assert.Equal(2, gravada.Itens.Count);
        Assert.NotEqual(default, gravada.CriadaEm);
    }

    [Fact]
    public async Task Imprimir_debita_o_estoque_e_fecha_a_nota()
    {
        await using var db = await ComNotasAsync(TestDb.Nota(1, StatusNotaFiscal.Aberta, ("P001", 2)));
        var id = (await db.NotasFiscais.SingleAsync()).Id;
        var estoque = new EstoqueFalso();
        var controller = new NotasController(db, estoque);

        var resultado = await controller.Imprimir(id, default);

        var nota = Assert.IsType<NotaFiscalResponse>(Assert.IsType<OkObjectResult>(resultado.Result).Value);
        Assert.Equal("Fechada", nota.Status);
        Assert.NotNull(nota.ImpressaEm);

        var gravada = await db.NotasFiscais.SingleAsync();
        Assert.Equal(StatusNotaFiscal.Fechada, gravada.Status);
        Assert.NotNull(gravada.ImpressaEm);
    }

    [Fact]
    public async Task Imprimir_usa_a_referencia_nota_id_e_manda_todos_os_itens_de_uma_vez()
    {
        await using var db = await ComNotasAsync(
            TestDb.Nota(1, StatusNotaFiscal.Aberta, ("P001", 2), ("P002", 3)));
        var id = (await db.NotasFiscais.SingleAsync()).Id;
        var estoque = new EstoqueFalso();
        var controller = new NotasController(db, estoque);

        await controller.Imprimir(id, default);

        // A referencia e o que torna a baixa idempotente do lado do Estoque.
        Assert.Equal($"nota-{id}", estoque.Recebido!.Referencia);
        Assert.Equal(1, estoque.Chamadas);
        Assert.Equal(2, estoque.Recebido.Itens.Count);
        Assert.Contains(estoque.Recebido.Itens, i => i.ProdutoCodigo == "P001" && i.Quantidade == 2);
        Assert.Contains(estoque.Recebido.Itens, i => i.ProdutoCodigo == "P002" && i.Quantidade == 3);
    }

    [Fact]
    public async Task Imprimir_nota_ja_fechada_lanca_NotaNaoAberta_e_nao_chama_o_estoque()
    {
        // O brief e explicito: so notas Abertas podem ser impressas.
        await using var db = await ComNotasAsync(TestDb.Nota(1, StatusNotaFiscal.Fechada, ("P001", 2)));
        var id = (await db.NotasFiscais.SingleAsync()).Id;
        var estoque = new EstoqueFalso();
        var controller = new NotasController(db, estoque);

        var erro = await Assert.ThrowsAsync<NotaNaoAbertaException>(() => controller.Imprimir(id, default));

        Assert.Equal(StatusCodes.Status409Conflict, erro.StatusCode);
        Assert.Equal(0, estoque.Chamadas);
    }

    [Fact]
    public async Task Imprimir_inexistente_devolve_404_e_nao_chama_o_estoque()
    {
        await using var db = TestDb.Criar();
        var estoque = new EstoqueFalso();
        var controller = new NotasController(db, estoque);

        var resultado = await controller.Imprimir(999, default);

        var problema = Assert.IsType<ProblemDetails>(Assert.IsType<ObjectResult>(resultado.Result).Value);
        Assert.Equal(StatusCodes.Status404NotFound, problema.Status);
        Assert.Equal(0, estoque.Chamadas);
    }

    [Fact]
    public async Task Recusa_do_estoque_deixa_a_nota_Aberta()
    {
        // Debitar primeiro e fechar depois: a falha nao pode deixar nota Fechada sem baixa.
        await using var db = await ComNotasAsync(TestDb.Nota(1, StatusNotaFiscal.Aberta, ("P001", 999)));
        var id = (await db.NotasFiscais.SingleAsync()).Id;
        var estoque = new EstoqueFalso
        {
            Erro = new EstoqueRecusouException(
                StatusCodes.Status422UnprocessableEntity,
                "Saldo insuficiente para P001."),
        };
        var controller = new NotasController(db, estoque);

        var erro = await Assert.ThrowsAsync<EstoqueRecusouException>(() => controller.Imprimir(id, default));

        Assert.Equal(StatusCodes.Status422UnprocessableEntity, erro.StatusCode);
        Assert.Contains("P001", erro.Message);

        db.ChangeTracker.Clear();
        var gravada = await db.NotasFiscais.SingleAsync();
        Assert.Equal(StatusNotaFiscal.Aberta, gravada.Status);
        Assert.Null(gravada.ImpressaEm);
    }

    [Fact]
    public async Task Falha_de_contrato_do_estoque_tambem_deixa_a_nota_Aberta()
    {
        await using var db = await ComNotasAsync(TestDb.Nota(1, StatusNotaFiscal.Aberta, ("P001", 1)));
        var id = (await db.NotasFiscais.SingleAsync()).Id;
        var estoque = new EstoqueFalso { Erro = new EstoqueRespostaInvalidaException("O Estoque respondeu 503.") };
        var controller = new NotasController(db, estoque);

        await Assert.ThrowsAsync<EstoqueRespostaInvalidaException>(() => controller.Imprimir(id, default));

        db.ChangeTracker.Clear();
        Assert.Equal(StatusNotaFiscal.Aberta, (await db.NotasFiscais.SingleAsync()).Status);
    }

    [Fact]
    public async Task Reimprimir_apos_sucesso_e_recusado_pelo_status()
    {
        await using var db = await ComNotasAsync(TestDb.Nota(1, StatusNotaFiscal.Aberta, ("P001", 2)));
        var id = (await db.NotasFiscais.SingleAsync()).Id;
        var estoque = new EstoqueFalso();
        var controller = new NotasController(db, estoque);

        await controller.Imprimir(id, default);
        await Assert.ThrowsAsync<NotaNaoAbertaException>(() => controller.Imprimir(id, default));

        // O estoque so foi chamado na primeira impressao.
        Assert.Equal(1, estoque.Chamadas);
    }
}
