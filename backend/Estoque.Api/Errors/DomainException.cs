namespace Estoque.Api.Errors;

/// <summary>
/// Base for expected, business-level refusals. Controllers and services throw these; the
/// global handler turns them into RFC 7807 ProblemDetails, so no endpoint builds an error body.
/// </summary>
public abstract class DomainException(string message) : Exception(message)
{
    public abstract int StatusCode { get; }

    public abstract string Title { get; }

    /// <summary>Structured detail merged into the ProblemDetails payload, for clients that want more than prose.</summary>
    public Dictionary<string, object?> Extensions { get; } = [];
}

public sealed class RecursoNaoEncontradoException(string message) : DomainException(message)
{
    public override int StatusCode => StatusCodes.Status404NotFound;

    public override string Title => "Recurso não encontrado";
}

public sealed class CodigoDuplicadoException : DomainException
{
    public CodigoDuplicadoException(string codigo)
        : base($"Já existe um produto com o código '{codigo}'.")
    {
        Extensions["codigo"] = codigo;
    }

    public override int StatusCode => StatusCodes.Status409Conflict;

    public override string Title => "Código já cadastrado";
}

public sealed class ProdutoDesconhecidoException : DomainException
{
    public ProdutoDesconhecidoException(IReadOnlyCollection<string> codigos)
        : base($"Produto(s) não cadastrado(s): {string.Join(", ", codigos)}.")
    {
        Extensions["codigosDesconhecidos"] = codigos;
    }

    public override int StatusCode => StatusCodes.Status422UnprocessableEntity;

    public override string Title => "Produto não cadastrado";
}

public sealed record SaldoInsuficiente(string ProdutoCodigo, int SaldoDisponivel, int QuantidadeSolicitada);

public sealed class SaldoInsuficienteException : DomainException
{
    public SaldoInsuficienteException(IReadOnlyCollection<SaldoInsuficiente> faltas)
        : base(Descrever(faltas))
    {
        Extensions["faltas"] = faltas;
    }

    public override int StatusCode => StatusCodes.Status422UnprocessableEntity;

    public override string Title => "Saldo insuficiente";

    private static string Descrever(IReadOnlyCollection<SaldoInsuficiente> faltas) =>
        "Saldo insuficiente para " + string.Join("; ", faltas.Select(f =>
            $"{f.ProdutoCodigo} (disponível {f.SaldoDisponivel}, solicitado {f.QuantidadeSolicitada})")) + ".";
}

public sealed class ConflitoDeConcorrenciaException : DomainException
{
    public ConflitoDeConcorrenciaException(IReadOnlyCollection<string> codigos)
        : base(codigos.Count > 0
            ? $"O saldo de {string.Join(", ", codigos)} foi alterado por outra operação. Tente novamente."
            : "O registro foi alterado por outra operação. Tente novamente.")
    {
        Extensions["codigosEmConflito"] = codigos;
    }

    public override int StatusCode => StatusCodes.Status409Conflict;

    public override string Title => "Conflito de concorrência";
}
