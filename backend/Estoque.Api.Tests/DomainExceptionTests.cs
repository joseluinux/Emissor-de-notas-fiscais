using Estoque.Api.Errors;

namespace Estoque.Api.Tests;

/// <summary>
/// Cada excecao carrega o status, o titulo e as extensions que o handler global copia para o
/// ProblemDetails. Sao elas que definem o que o usuario le, entao valem teste proprio.
/// </summary>
public class DomainExceptionTests
{
    [Fact]
    public void RecursoNaoEncontrado_e_404()
    {
        var erro = new RecursoNaoEncontradoException("Produto 9 não encontrado.");

        Assert.Equal(StatusCodes.Status404NotFound, erro.StatusCode);
        Assert.Equal("Recurso não encontrado", erro.Title);
        Assert.Equal("Produto 9 não encontrado.", erro.Message);
        Assert.Empty(erro.Extensions);
    }

    [Fact]
    public void CodigoDuplicado_e_409_e_expoe_o_codigo()
    {
        var erro = new CodigoDuplicadoException("P001");

        Assert.Equal(StatusCodes.Status409Conflict, erro.StatusCode);
        Assert.Equal("Código já cadastrado", erro.Title);
        Assert.Contains("P001", erro.Message);
        Assert.Equal("P001", erro.Extensions["codigo"]);
    }

    [Fact]
    public void ProdutoDesconhecido_e_422_e_lista_todos_os_codigos()
    {
        var erro = new ProdutoDesconhecidoException(["P404", "P405"]);

        Assert.Equal(StatusCodes.Status422UnprocessableEntity, erro.StatusCode);
        Assert.Equal("Produto não cadastrado", erro.Title);
        Assert.Contains("P404", erro.Message);
        Assert.Contains("P405", erro.Message);

        var codigos = Assert.IsAssignableFrom<IReadOnlyCollection<string>>(erro.Extensions["codigosDesconhecidos"]);
        Assert.Equal(2, codigos.Count);
    }

    [Fact]
    public void SaldoInsuficiente_e_422_e_descreve_disponivel_e_solicitado()
    {
        var erro = new SaldoInsuficienteException([new SaldoInsuficiente("P001", SaldoDisponivel: 1, QuantidadeSolicitada: 5)]);

        Assert.Equal(StatusCodes.Status422UnprocessableEntity, erro.StatusCode);
        Assert.Equal("Saldo insuficiente", erro.Title);
        Assert.Contains("P001", erro.Message);
        Assert.Contains("1", erro.Message);
        Assert.Contains("5", erro.Message);
    }

    [Fact]
    public void SaldoInsuficiente_junta_varias_faltas_numa_mensagem_so()
    {
        var erro = new SaldoInsuficienteException(
        [
            new SaldoInsuficiente("P001", 1, 5),
            new SaldoInsuficiente("P002", 0, 3),
        ]);

        Assert.Contains("P001", erro.Message);
        Assert.Contains("P002", erro.Message);
        Assert.EndsWith(".", erro.Message);
    }

    [Fact]
    public void ConflitoDeConcorrencia_e_409_e_nomeia_os_produtos()
    {
        var erro = new ConflitoDeConcorrenciaException(["P001"]);

        Assert.Equal(StatusCodes.Status409Conflict, erro.StatusCode);
        Assert.Equal("Conflito de concorrência", erro.Title);
        Assert.Contains("P001", erro.Message);
        Assert.Contains("Tente novamente", erro.Message);
    }

    [Fact]
    public void ConflitoDeConcorrencia_sem_codigos_ainda_da_uma_mensagem_util()
    {
        // O EF nem sempre diz qual entidade perdeu a corrida; a mensagem nao pode ficar vazia.
        var erro = new ConflitoDeConcorrenciaException([]);

        Assert.Contains("alterado por outra operação", erro.Message);
        Assert.Contains("Tente novamente", erro.Message);
    }

    [Fact]
    public void Todas_derivam_de_DomainException()
    {
        // O handler global filtra por este tipo base — sair da hierarquia vira 500 silencioso.
        Assert.IsAssignableFrom<DomainException>(new RecursoNaoEncontradoException("x"));
        Assert.IsAssignableFrom<DomainException>(new CodigoDuplicadoException("x"));
        Assert.IsAssignableFrom<DomainException>(new ProdutoDesconhecidoException(["x"]));
        Assert.IsAssignableFrom<DomainException>(new SaldoInsuficienteException([new SaldoInsuficiente("x", 0, 1)]));
        Assert.IsAssignableFrom<DomainException>(new ConflitoDeConcorrenciaException(["x"]));
    }
}
