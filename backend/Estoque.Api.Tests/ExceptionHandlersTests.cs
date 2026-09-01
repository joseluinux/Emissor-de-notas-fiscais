using Estoque.Api.Errors;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging.Abstractions;

namespace Estoque.Api.Tests;

public class ExceptionHandlersTests
{
    /// <summary>Captura o ProblemDetails escrito, que e o que o cliente realmente recebe.</summary>
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
        public string ApplicationName { get; set; } = "Estoque.Api.Tests";
        public string ContentRootPath { get; set; } = AppContext.BaseDirectory;
        public IFileProvider ContentRootFileProvider { get; set; } = new NullFileProvider();
    }

    [Fact]
    public async Task DomainExceptionHandler_traduz_a_recusa_para_ProblemDetails()
    {
        var spy = new ProblemDetailsSpy();
        var handler = new DomainExceptionHandler(spy);
        var contexto = new DefaultHttpContext();

        var tratado = await handler.TryHandleAsync(contexto, new CodigoDuplicadoException("P001"), default);

        Assert.True(tratado);
        Assert.Equal(StatusCodes.Status409Conflict, contexto.Response.StatusCode);
        Assert.NotNull(spy.Escrito);
        Assert.Equal(StatusCodes.Status409Conflict, spy.Escrito.Status);
        Assert.Equal("Código já cadastrado", spy.Escrito.Title);
        Assert.Contains("P001", spy.Escrito.Detail);
    }

    [Fact]
    public async Task DomainExceptionHandler_copia_as_extensions_da_excecao()
    {
        var spy = new ProblemDetailsSpy();
        var handler = new DomainExceptionHandler(spy);

        await handler.TryHandleAsync(
            new DefaultHttpContext(),
            new SaldoInsuficienteException([new SaldoInsuficiente("P001", 1, 5)]),
            default);

        // O cliente que quiser mais do que a prosa le as faltas estruturadas.
        Assert.True(spy.Escrito!.Extensions.ContainsKey("faltas"));
    }

    [Fact]
    public async Task DomainExceptionHandler_ignora_o_que_nao_e_recusa_de_dominio()
    {
        var spy = new ProblemDetailsSpy();
        var handler = new DomainExceptionHandler(spy);
        var contexto = new DefaultHttpContext();

        var tratado = await handler.TryHandleAsync(contexto, new InvalidOperationException("boom"), default);

        // Devolver false e o que deixa o UnhandledExceptionHandler assumir.
        Assert.False(tratado);
        Assert.Null(spy.Escrito);
        Assert.Equal(StatusCodes.Status200OK, contexto.Response.StatusCode);
    }

    [Fact]
    public async Task UnhandledExceptionHandler_devolve_500_com_correlationId()
    {
        var spy = new ProblemDetailsSpy();
        var handler = new UnhandledExceptionHandler(
            spy,
            new Ambiente(Environments.Production),
            NullLogger<UnhandledExceptionHandler>.Instance);
        var contexto = new DefaultHttpContext();

        var tratado = await handler.TryHandleAsync(contexto, new InvalidOperationException("boom"), default);

        Assert.True(tratado);
        Assert.Equal(StatusCodes.Status500InternalServerError, contexto.Response.StatusCode);
        Assert.Equal("Erro interno", spy.Escrito!.Title);
        Assert.True(spy.Escrito.Extensions.ContainsKey("correlationId"));
        Assert.NotNull(spy.Escrito.Extensions["correlationId"]);
    }

    [Fact]
    public async Task UnhandledExceptionHandler_nao_vaza_stack_trace_fora_de_Development()
    {
        var spy = new ProblemDetailsSpy();
        var handler = new UnhandledExceptionHandler(
            spy,
            new Ambiente(Environments.Production),
            NullLogger<UnhandledExceptionHandler>.Instance);

        await handler.TryHandleAsync(new DefaultHttpContext(), new InvalidOperationException("segredo"), default);

        Assert.DoesNotContain("segredo", spy.Escrito!.Detail);
        Assert.Contains("correlationId ao suporte", spy.Escrito.Detail);
    }

    [Fact]
    public async Task UnhandledExceptionHandler_detalha_a_excecao_em_Development()
    {
        var spy = new ProblemDetailsSpy();
        var handler = new UnhandledExceptionHandler(
            spy,
            new Ambiente(Environments.Development),
            NullLogger<UnhandledExceptionHandler>.Instance);

        await handler.TryHandleAsync(new DefaultHttpContext(), new InvalidOperationException("boom"), default);

        Assert.Contains("boom", spy.Escrito!.Detail);
        Assert.Contains(nameof(InvalidOperationException), spy.Escrito.Detail);
    }
}
