using Faturamento.Api.Domain;
using Faturamento.Api.Infrastructure;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging.Abstractions;

namespace Faturamento.Api.Tests;

public class DominioExceptionHandlerTests
{
    private sealed class ProblemDetailsSpy : IProblemDetailsService
    {
        public ProblemDetails? Escrito { get; private set; }

        public ValueTask<bool> TryWriteAsync(ProblemDetailsContext context)
        {
            Escrito = context.ProblemDetails;
            return ValueTask.FromResult(true);
        }

        public ValueTask WriteAsync(ProblemDetailsContext context)
        {
            Escrito = context.ProblemDetails;
            return ValueTask.CompletedTask;
        }
    }

    private sealed class Ambiente(string nome) : IHostEnvironment
    {
        public string EnvironmentName { get; set; } = nome;
        public string ApplicationName { get; set; } = "Faturamento.Api.Tests";
        public string ContentRootPath { get; set; } = AppContext.BaseDirectory;
        public IFileProvider ContentRootFileProvider { get; set; } = new NullFileProvider();
    }

    private static (DominioExceptionHandler Handler, ProblemDetailsSpy Spy) Montar(string ambiente = "Production")
    {
        var spy = new ProblemDetailsSpy();
        var handler = new DominioExceptionHandler(
            spy,
            new Ambiente(ambiente),
            NullLogger<DominioExceptionHandler>.Instance);

        return (handler, spy);
    }

    [Fact]
    public async Task NotaNaoAberta_vira_409_com_o_numero_e_o_status_na_mensagem()
    {
        var (handler, spy) = Montar();
        var contexto = new DefaultHttpContext();
        var nota = TestDb.Nota(42, StatusNotaFiscal.Fechada);

        var tratado = await handler.TryHandleAsync(contexto, new NotaNaoAbertaException(nota), default);

        Assert.True(tratado);
        Assert.Equal(StatusCodes.Status409Conflict, contexto.Response.StatusCode);
        Assert.Equal("Requisicao rejeitada", spy.Escrito!.Title);
        Assert.Contains("42", spy.Escrito.Detail);
        Assert.Contains("Fechada", spy.Escrito.Detail);
    }

    [Fact]
    public async Task Recusa_do_estoque_atravessa_com_o_status_recebido()
    {
        var (handler, spy) = Montar();
        var contexto = new DefaultHttpContext();

        await handler.TryHandleAsync(
            contexto,
            new EstoqueRecusouException(StatusCodes.Status422UnprocessableEntity, "Saldo insuficiente para P001."),
            default);

        // 422 do Estoque continua 422 no Faturamento, com o detail original.
        Assert.Equal(StatusCodes.Status422UnprocessableEntity, contexto.Response.StatusCode);
        Assert.Equal("Saldo insuficiente para P001.", spy.Escrito!.Detail);
    }

    [Fact]
    public async Task Toda_resposta_carrega_um_correlationId()
    {
        var (handler, spy) = Montar();

        await handler.TryHandleAsync(new DefaultHttpContext(), new InvalidOperationException("boom"), default);

        Assert.True(spy.Escrito!.Extensions.ContainsKey("correlationId"));
        Assert.NotNull(spy.Escrito.Extensions["correlationId"]);
    }

    [Fact]
    public async Task Excecao_inesperada_vira_500_sem_vazar_a_mensagem_fora_de_Development()
    {
        var (handler, spy) = Montar(Environments.Production);
        var contexto = new DefaultHttpContext();

        await handler.TryHandleAsync(contexto, new InvalidOperationException("segredo"), default);

        Assert.Equal(StatusCodes.Status500InternalServerError, contexto.Response.StatusCode);
        Assert.Equal("Erro interno", spy.Escrito!.Title);
        Assert.DoesNotContain("segredo", spy.Escrito.Detail);
        Assert.False(spy.Escrito.Extensions.ContainsKey("exception"));
    }

    [Fact]
    public async Task Em_Development_a_excecao_completa_aparece_numa_extension()
    {
        var (handler, spy) = Montar(Environments.Development);

        await handler.TryHandleAsync(new DefaultHttpContext(), new InvalidOperationException("boom"), default);

        Assert.Contains("boom", Assert.IsType<string>(spy.Escrito!.Extensions["exception"]));
    }

    [Fact]
    public async Task Contrato_quebrado_do_estoque_vira_500_e_nao_4xx()
    {
        // EstoqueRespostaInvalidaException nao deriva de DominioException de proposito.
        var (handler, spy) = Montar();
        var contexto = new DefaultHttpContext();

        await handler.TryHandleAsync(
            contexto,
            new EstoqueRespostaInvalidaException("O Estoque respondeu 503."),
            default);

        Assert.Equal(StatusCodes.Status500InternalServerError, contexto.Response.StatusCode);
        Assert.Equal("Erro interno", spy.Escrito!.Title);
    }

    [Fact]
    public void Toda_recusa_de_negocio_deriva_de_DominioException()
    {
        // O handler filtra por este tipo base. Uma excecao fora da hierarquia nao ganha o
        // correlationId e nao gera linha de log — foi exatamente assim que o 404 da nota
        // inexistente ficou invisivel no servidor antes de virar NotaNaoEncontradaException.
        Assert.IsAssignableFrom<DominioException>(new NotaNaoAbertaException(TestDb.Nota(1)));
        Assert.IsAssignableFrom<DominioException>(new EstoqueRecusouException(409, "x"));
        Assert.IsAssignableFrom<DominioException>(new NotaNaoEncontradaException(999));
    }

    [Fact]
    public async Task NotaNaoEncontrada_vira_404_com_correlationId()
    {
        // A regressao que este fix corrige: antes o 404 saia pelo controller, sem passar
        // pelo handler, entao nao tinha correlationId nem log.
        var (handler, spy) = Montar();
        var contexto = new DefaultHttpContext();

        await handler.TryHandleAsync(contexto, new NotaNaoEncontradaException(999), default);

        Assert.Equal(StatusCodes.Status404NotFound, contexto.Response.StatusCode);
        Assert.Equal(StatusCodes.Status404NotFound, spy.Escrito!.Status);
        Assert.Contains("999", spy.Escrito.Detail);
        Assert.True(spy.Escrito.Extensions.ContainsKey("correlationId"));
    }
}
