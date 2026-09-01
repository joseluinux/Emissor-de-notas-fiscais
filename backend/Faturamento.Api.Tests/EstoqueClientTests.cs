using System.Net;
using System.Text;
using Faturamento.Api.Domain;
using Faturamento.Api.Infrastructure;
using Microsoft.Extensions.Logging.Abstractions;

namespace Faturamento.Api.Tests;

/// <summary>
/// A fronteira entre os dois servicos. Nenhum teste aqui toca a rede: o HttpMessageHandler e
/// falso, entao cada resposta possivel do Estoque pode ser encenada.
/// </summary>
public class EstoqueClientTests
{
    private sealed class RespostaFixa(HttpStatusCode status, string? corpo, string tipo = "application/json")
        : HttpMessageHandler
    {
        public HttpRequestMessage? Requisicao { get; private set; }

        protected override async Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            Requisicao = request;
            // Le o corpo agora: depois que a resposta volta, o conteudo da requisicao ja foi descartado.
            if (request.Content is not null)
            {
                CorpoEnviado = await request.Content.ReadAsStringAsync(cancellationToken);
            }

            return new HttpResponseMessage(status)
            {
                Content = corpo is null
                    ? new StringContent(string.Empty)
                    : new StringContent(corpo, Encoding.UTF8, tipo),
            };
        }

        public string? CorpoEnviado { get; private set; }
    }

    private static (EstoqueClient Cliente, RespostaFixa Handler) Montar(
        HttpStatusCode status,
        string? corpo,
        string tipo = "application/json")
    {
        var handler = new RespostaFixa(status, corpo, tipo);
        var http = new HttpClient(handler) { BaseAddress = new Uri("http://estoque.local") };

        return (new EstoqueClient(http, NullLogger<EstoqueClient>.Instance), handler);
    }

    private static RegistrarMovimentacaoRequest Baixa() =>
        new("nota-1", [new MovimentacaoItemRequest("P001", 2)]);

    private const string CorpoOk = """
        {"id":1,"referencia":"nota-1","criadaEm":"2026-08-26T23:59:29Z",
         "itens":[{"produtoCodigo":"P001","quantidade":2,"saldoResultante":8}]}
        """;

    [Fact]
    public async Task Baixa_criada_201_devolve_a_movimentacao()
    {
        var (cliente, _) = Montar(HttpStatusCode.Created, CorpoOk);

        var resposta = await cliente.RegistrarBaixaAsync(Baixa(), default);

        Assert.Equal(1, resposta.Id);
        Assert.Equal("nota-1", resposta.Referencia);
        Assert.Equal(8, resposta.Itens.Single().SaldoResultante);
    }

    [Fact]
    public async Task Replay_200_tambem_e_sucesso()
    {
        // 201 na primeira baixa, 200 quando o Estoque replica. Os dois devolvem o mesmo corpo.
        var (cliente, _) = Montar(HttpStatusCode.OK, CorpoOk);

        var resposta = await cliente.RegistrarBaixaAsync(Baixa(), default);

        Assert.Equal("nota-1", resposta.Referencia);
    }

    [Fact]
    public async Task Envia_a_referencia_e_os_itens_no_corpo()
    {
        var (cliente, handler) = Montar(HttpStatusCode.Created, CorpoOk);

        await cliente.RegistrarBaixaAsync(
            new RegistrarMovimentacaoRequest("nota-9", [new MovimentacaoItemRequest("P007", 3)]),
            default);

        Assert.Equal(HttpMethod.Post, handler.Requisicao!.Method);
        Assert.EndsWith("api/estoque/movimentacoes", handler.Requisicao.RequestUri!.ToString());
        Assert.Contains("nota-9", handler.CorpoEnviado);
        Assert.Contains("P007", handler.CorpoEnviado);
    }

    [Fact]
    public async Task Sucesso_com_corpo_vazio_vira_EstoqueRespostaInvalida()
    {
        // Contrato quebrado, nao regra de negocio: precisa virar 500, nao 422.
        var (cliente, _) = Montar(HttpStatusCode.OK, "null");

        var erro = await Assert.ThrowsAsync<EstoqueRespostaInvalidaException>(
            () => cliente.RegistrarBaixaAsync(Baixa(), default));

        Assert.Contains("corpo vazio", erro.Message);
    }

    [Theory]
    [InlineData(HttpStatusCode.UnprocessableEntity, 422)]
    [InlineData(HttpStatusCode.Conflict, 409)]
    public async Task Recusa_de_regra_atravessa_com_status_e_detail_intactos(HttpStatusCode status, int esperado)
    {
        var problema = """
            {"title":"Saldo insuficiente","status":422,
             "detail":"Saldo insuficiente para P001 (disponível 1, solicitado 5)."}
            """;
        var (cliente, _) = Montar(status, problema, "application/problem+json");

        var erro = await Assert.ThrowsAsync<EstoqueRecusouException>(
            () => cliente.RegistrarBaixaAsync(Baixa(), default));

        // O usuario le exatamente o motivo do Estoque, sem o Faturamento reescrever a mensagem.
        Assert.Equal(esperado, erro.StatusCode);
        Assert.Contains("P001", erro.Message);
        Assert.Contains("disponível 1", erro.Message);
    }

    [Fact]
    public async Task Recusa_sem_detail_cai_numa_mensagem_generica()
    {
        var (cliente, _) = Montar(HttpStatusCode.UnprocessableEntity, """{"title":"Recusado"}""");

        var erro = await Assert.ThrowsAsync<EstoqueRecusouException>(
            () => cliente.RegistrarBaixaAsync(Baixa(), default));

        Assert.Contains("nao informou o motivo", erro.Message);
    }

    [Fact]
    public async Task Recusa_com_corpo_fora_do_formato_ProblemDetails_nao_quebra_o_cliente()
    {
        var (cliente, _) = Montar(HttpStatusCode.Conflict, "<html>oops</html>", "text/html");

        var erro = await Assert.ThrowsAsync<EstoqueRecusouException>(
            () => cliente.RegistrarBaixaAsync(Baixa(), default));

        Assert.Equal(409, erro.StatusCode);
        Assert.Contains("nao informou o motivo", erro.Message);
    }

    [Theory]
    [InlineData(HttpStatusCode.BadRequest)]
    [InlineData(HttpStatusCode.NotFound)]
    [InlineData(HttpStatusCode.InternalServerError)]
    [InlineData(HttpStatusCode.ServiceUnavailable)]
    public async Task Qualquer_outra_resposta_vira_EstoqueRespostaInvalida(HttpStatusCode status)
    {
        // Requisito obrigatorio 2 esta adiado: hoje isso vira 500, nao um 503 amigavel.
        var (cliente, _) = Montar(status, """{"erro":"qualquer"}""");

        var erro = await Assert.ThrowsAsync<EstoqueRespostaInvalidaException>(
            () => cliente.RegistrarBaixaAsync(Baixa(), default));

        Assert.Contains(((int)status).ToString(), erro.Message);
    }

    [Fact]
    public void EstoqueRespostaInvalida_nao_e_recusa_de_dominio()
    {
        // Se herdasse de DominioException viraria 4xx e esconderia um contrato quebrado.
        Assert.IsNotAssignableFrom<DominioException>(new EstoqueRespostaInvalidaException("x"));
    }
}
