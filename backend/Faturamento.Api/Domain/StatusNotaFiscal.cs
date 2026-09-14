namespace Faturamento.Api.Domain;

/// <summary>
/// Invoice lifecycle. Only Aberta can be printed; printing is what makes it Fechada, and there is
/// no way back. Persisted as text, so the order of these members carries no meaning in the database.
/// </summary>
public enum StatusNotaFiscal
{
    Aberta,
    Fechada
}
