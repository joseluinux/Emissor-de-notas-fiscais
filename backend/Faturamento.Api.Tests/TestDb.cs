using System.Runtime.CompilerServices;
using Faturamento.Api.Data;
using Faturamento.Api.Domain;
using Microsoft.EntityFrameworkCore;

namespace Faturamento.Api.Tests;

/// <summary>
/// Banco InMemory descartavel, um por teste, para que a ordem de execucao nunca importe.
///
/// Limite conhecido do provider: <see cref="NotaFiscal.Numero"/> vem da sequence do Postgres
/// (HasDefaultValueSql), que o InMemory ignora — aqui o Numero fica 0 a menos que o teste o
/// atribua. A numeracao sequencial de verdade e verificada contra o Postgres, nao neste projeto.
/// </summary>
internal static class TestDb
{
    public static AppDbContext Criar([CallerMemberName] string nome = "")
        => new(new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase($"{nome}-{Guid.NewGuid()}")
            .Options);

    public static NotaFiscal Nota(
        int numero,
        StatusNotaFiscal status = StatusNotaFiscal.Aberta,
        params (string Codigo, int Quantidade)[] itens)
    {
        var nota = new NotaFiscal
        {
            Numero = numero,
            Status = status,
            CriadaEm = DateTime.UtcNow,
        };

        foreach (var (codigo, quantidade) in itens)
        {
            nota.Itens.Add(new NotaFiscalItem
            {
                ProdutoCodigo = codigo,
                Descricao = $"Produto {codigo}",
                Quantidade = quantidade,
            });
        }

        return nota;
    }
}
