using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Faturamento.Api.Domain;
using Microsoft.AspNetCore.Mvc;

namespace Faturamento.Api.Infrastructure;

/// <summary>
/// Typed client for the Estoque debit endpoint. Registered through AddHttpClient with the base
/// address from configuration, and deliberately without retry, circuit breaker or a timeout of
/// its own — see the comment on the failure path below.
/// </summary>
public class EstoqueClient(HttpClient http, ILogger<EstoqueClient> logger) : IEstoqueClient
{
    private const string Rota = "api/estoque/movimentacoes";

    /// <inheritdoc />
    public async Task<MovimentacaoEstoqueResponse> RegistrarBaixaAsync(
        RegistrarMovimentacaoRequest request,
        CancellationToken cancellationToken)
    {
        // Logged before the call, with the reference: it is the key that ties this line to the
        // movement on the Estoque side when a print has to be traced across both services.
        logger.LogInformation(
            "Solicitando baixa no Estoque. Referencia={Referencia}, Itens={Itens}",
            request.Referencia,
            request.Itens.Count);

        var resposta = await http.PostAsJsonAsync(Rota, request, cancellationToken);

        // 201 on the first debit, 200 when Estoque replays a debit already recorded under this same
        // reference. Both are success and return the same body, so neither is special-cased here.
        if (resposta.IsSuccessStatusCode)
        {
            return await resposta.Content.ReadFromJsonAsync<MovimentacaoEstoqueResponse>(cancellationToken)
                ?? throw new EstoqueRespostaInvalidaException(
                    $"O Estoque respondeu {(int)resposta.StatusCode} com corpo vazio.");
        }

        // Business refusals cross over with their status and detail intact. Estoque already knows
        // which product fell short; rewriting the message here would only lose that.
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

        // Any other response becomes a 500. A transport failure does not even reach this point: the
        // exception propagates straight out and also becomes a 500. Turning that into a friendly
        // 503 is mandatory requirement 2, deliberately deferred — see "Deferred" in NOTES.md.
        throw new EstoqueRespostaInvalidaException(
            $"O Estoque respondeu {(int)resposta.StatusCode} para {Rota}.");
    }

    /// <summary>Pulls the ProblemDetails detail out of a refusal, falling back to a generic message.</summary>
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
            // Body not in ProblemDetails shape: fall through to the generic message below. A refusal
            // must never be downgraded into a 500 just because its body could not be parsed.
        }

        return "O Estoque recusou a baixa e nao informou o motivo.";
    }
}
