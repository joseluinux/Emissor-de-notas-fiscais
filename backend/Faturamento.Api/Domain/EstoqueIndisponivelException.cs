namespace Faturamento.Api.Domain;

/// <summary>
/// O Estoque nao respondeu: processo fora do ar, rede caida, timeout, ou circuito aberto depois
/// de falhas seguidas. Vira 503 com mensagem legivel, e a nota continua Aberta — o usuario tenta
/// de novo quando o servico voltar.
///
/// Deriva de <see cref="DominioException"/> de proposito, ao contrario de
/// <c>EstoqueRespostaInvalidaException</c>: indisponibilidade e uma condicao esperada de um
/// sistema distribuido, com resposta util para quem chamou, e nao um contrato quebrado que exige
/// investigacao. A causa vai como InnerException para o log dizer se foi recusa de conexao,
/// timeout ou circuito aberto.
/// </summary>
public sealed class EstoqueIndisponivelException(Exception causa)
    : DominioException(
        "O servico de Estoque esta indisponivel no momento. A nota continua Aberta — "
            + "tente imprimir novamente em instantes.",
        StatusCodes.Status503ServiceUnavailable,
        causa);
