namespace Estoque.Api.Errors;

/// <summary>
/// Base for expected, business-level refusals. Controllers and services throw these; the
/// global handler turns them into RFC 7807 ProblemDetails, so no endpoint builds an error body.
/// </summary>
public abstract class DomainException(string message) : Exception(message)
{
    /// <summary>HTTP status the refusal maps to. Always 4xx — these are expected outcomes, not faults.</summary>
    public abstract int StatusCode { get; }

    /// <summary>Short, stable title for the ProblemDetails payload.</summary>
    public abstract string Title { get; }

    /// <summary>Structured detail merged into the ProblemDetails payload, for clients that want more than prose.</summary>
    public Dictionary<string, object?> Extensions { get; } = [];
}

/// <summary>The addressed product or movement does not exist.</summary>
public sealed class RecursoNaoEncontradoException(string message) : DomainException(message)
{
    public override int StatusCode => StatusCodes.Status404NotFound;

    public override string Title => "Recurso não encontrado";
}

/// <summary>The code is already registered, so the product cannot be created.</summary>
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

/// <summary>
/// A debit named codes that are not registered. Carries all of them at once so the caller can fix
/// the whole request in one round trip.
/// </summary>
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

/// <summary>One shortfall: what was asked for against what was there.</summary>
public sealed record SaldoInsuficiente(string ProdutoCodigo, int SaldoDisponivel, int QuantidadeSolicitada);

/// <summary>
/// At least one line exceeds the available balance, so nothing was debited. Every shortfall travels
/// both in the message and in the <c>faltas</c> extension, so a UI can render them without parsing prose.
/// </summary>
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

/// <summary>
/// Someone else changed one of these rows first, caught by the xmin row version. Nothing was
/// written, so retrying the same request is safe — hence the message says so.
/// </summary>
public sealed class ConflitoDeConcorrenciaException : DomainException
{
    // Two wordings because the codes are not always known: the debit path collects them from the
    // failed entries, which can come back empty.
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
