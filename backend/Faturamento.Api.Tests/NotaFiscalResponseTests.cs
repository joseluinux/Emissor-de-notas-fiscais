using Faturamento.Api.Domain;
using Faturamento.Api.Dtos;

namespace Faturamento.Api.Tests;

public class NotaFiscalResponseTests
{
    [Fact]
    public void Copia_os_campos_da_nota()
    {
        var criadaEm = new DateTime(2026, 8, 26, 23, 59, 29, DateTimeKind.Utc);
        var nota = new NotaFiscal
        {
            Id = 7,
            Numero = 42,
            Status = StatusNotaFiscal.Aberta,
            CriadaEm = criadaEm,
            ImpressaEm = null,
        };

        var resposta = NotaFiscalResponse.De(nota);

        Assert.Equal(7, resposta.Id);
        Assert.Equal(42, resposta.Numero);
        Assert.Equal(criadaEm, resposta.CriadaEm);
        Assert.Null(resposta.ImpressaEm);
        Assert.Empty(resposta.Itens);
    }

    [Fact]
    public void Serializa_o_status_como_texto()
    {
        // O contrato do brief fala em Aberta/Fechada, nao no ordinal do enum.
        Assert.Equal("Aberta", NotaFiscalResponse.De(TestDb.Nota(1)).Status);
        Assert.Equal(
            "Fechada",
            NotaFiscalResponse.De(TestDb.Nota(1, StatusNotaFiscal.Fechada)).Status);
    }

    [Fact]
    public void Mapeia_os_itens_da_nota()
    {
        var nota = TestDb.Nota(1, StatusNotaFiscal.Aberta, ("P001", 2), ("P002", 3));

        var resposta = NotaFiscalResponse.De(nota);

        Assert.Equal(2, resposta.Itens.Count);
        Assert.Equal("P001", resposta.Itens[0].ProdutoCodigo);
        Assert.Equal(2, resposta.Itens[0].Quantidade);
        Assert.Equal("Produto P001", resposta.Itens[0].Descricao);
    }

    [Fact]
    public void Ordena_os_itens_por_id()
    {
        var nota = new NotaFiscal { Id = 1, Numero = 1 };
        nota.Itens.Add(new NotaFiscalItem { Id = 30, ProdutoCodigo = "P003", Descricao = "C", Quantidade = 1 });
        nota.Itens.Add(new NotaFiscalItem { Id = 10, ProdutoCodigo = "P001", Descricao = "A", Quantidade = 1 });
        nota.Itens.Add(new NotaFiscalItem { Id = 20, ProdutoCodigo = "P002", Descricao = "B", Quantidade = 1 });

        var resposta = NotaFiscalResponse.De(nota);

        // Ordem estavel: a nota impressa nao pode trocar a ordem das linhas a cada consulta.
        Assert.Equal(["P001", "P002", "P003"], resposta.Itens.Select(i => i.ProdutoCodigo));
    }

    [Fact]
    public void Leva_o_ImpressaEm_quando_a_nota_ja_foi_impressa()
    {
        var impressaEm = new DateTime(2026, 8, 27, 1, 0, 0, DateTimeKind.Utc);
        var nota = new NotaFiscal
        {
            Id = 1,
            Numero = 1,
            Status = StatusNotaFiscal.Fechada,
            ImpressaEm = impressaEm,
        };

        var resposta = NotaFiscalResponse.De(nota);

        Assert.Equal(impressaEm, resposta.ImpressaEm);
        Assert.Equal("Fechada", resposta.Status);
    }
}
