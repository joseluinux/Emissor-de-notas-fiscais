using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Faturamento.Api.Domain;
using Microsoft.AspNetCore.Mvc;

namespace Faturamento.Api.Infrastructure;

public class EstoqueClient(HttpClient http, ILogger<EstoqueClient> logger) : IEstoqueClient
{
    private const string Rota = "api/estoque/movimentacoes";

    public async Task<MovimentacaoEstoqueResponse> RegistrarBaixaAsync(
        RegistrarMovimentacaoRequest request,
        CancellationToken cancellationToken)
    {
        logger.LogInformation(
            "Solicitando baixa no Estoque. Referencia={Referencia}, Itens={Itens}",
            request.Referencia,
            request.Itens.Count);

        var resposta = await http.PostAsJsonAsync(Rota, request, cancellationToken);

        // 201 na primeira baixa, 200 quando o Estoque replica uma baixa ja registrada com esta
        // mesma referencia. Os dois sao sucesso e devolvem o mesmo corpo.
        if (resposta.IsSuccessStatusCode)
        {
            return await resposta.Content.ReadFromJsonAsync<MovimentacaoEstoqueResponse>(cancellationToken)
                ?? throw new EstoqueRespostaInvalidaException(
                    $"O Estoque respondeu {(int)resposta.StatusCode} com corpo vazio.");
        }

        // Recusas de regra atravessam com status e detail intactos.
        if (resposta.StatusCode is HttpStatusCode.UnprocessableEntity or HttpStatusCode.Conflict)
        {
            var detalhe = await LerDetalheAsync(resposta, cancellationToken);

            logger.LogWarning(
                "Estoque recusou a baixa. Referencia={Referencia}, Status={Status}, Detalhe={Detalhe}",
                request.Referencia,
                (int)resposta.StatusCode,
                detalhe);

            throw new EstoqueRecusouException((int)resposta.StatusCode, detalhe);
        }

        // Qualquer outra resposta vira 500. Falha de transporte nem chega aqui: a excecao sobe
        // direto e tambem vira 500. Transformar isso num 503 amigavel e o requisito obrigatorio
        // 2, deliberadamente adiado — ver "Deferred" no NOTES.md.
        throw new EstoqueRespostaInvalidaException(
            $"O Estoque respondeu {(int)resposta.StatusCode} para {Rota}.");
    }

    private static async Task<string> LerDetalheAsync(
        HttpResponseMessage resposta,
        CancellationToken cancellationToken)
    {
        try
        {
            var problema = await resposta.Content.ReadFromJsonAsync<ProblemDetails>(cancellationToken);

            if (!string.IsNullOrWhiteSpace(problema?.Detail))
            {
                return problema.Detail;
            }
        }
        catch (Exception e) when (e is JsonException or NotSupportedException)
        {
            // Corpo fora do formato ProblemDetails: cai na mensagem generica abaixo.
        }

        return "O Estoque recusou a baixa e nao informou o motivo.";
    }
}
