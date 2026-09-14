# Emissor de Notas Fiscais

[![CI](https://github.com/joseluinux/Emissor-de-notas-fiscais/actions/workflows/ci.yml/badge.svg)](https://github.com/joseluinux/Emissor-de-notas-fiscais/actions/workflows/ci.yml)

Sistema de emissão de notas fiscais com arquitetura de microsserviços: dois serviços
**ASP.NET Core 9** independentes, cada um com o seu próprio banco PostgreSQL, e um
front-end **Angular**.

| Serviço | Responsabilidade | Porta |
| --- | --- | --- |
| **Estoque** | Produtos e saldos | `:5001` |
| **Faturamento** | Notas fiscais e impressão | `:5002` |
| **Frontend** | SPA Angular | `:4200` |

O núcleo do sistema é uma operação que atravessa os dois serviços: **imprimir** uma nota
fiscal fecha a nota **e** debita do estoque a quantidade de cada item — o Faturamento chama
o Estoque por HTTP e precisa se comportar corretamente quando o saldo é insuficiente, quando
a mesma impressão é pedida duas vezes e quando o Estoque está fora do ar.

## Destaques técnicos

- **Idempotência** — a baixa de estoque é ancorada num índice único de `Referencia`, então
  reimprimir replica o resultado original em vez de debitar duas vezes.
- **Concorrência** — `Produto` usa a coluna de sistema `xmin` do PostgreSQL como token de
  concorrência: duas baixas simultâneas sobre o mesmo saldo geram um 409 limpo, nunca saldo
  negativo.
- **Tudo ou nada** — uma baixa com vários itens é aplicada por inteiro ou recusada por
  inteiro, e a recusa lista *todas* as faltas de uma vez.
- **Erros como ProblemDetails** — toda falha sai em RFC 7807, produzida por um
  `IExceptionHandler` global; nenhum controller monta corpo de erro.
- **Ordem importa** — debitar primeiro, fechar depois, para que uma falha nunca deixe uma
  nota fechada com o estoque não debitado.

## Como rodar

```bash
# PostgreSQL
docker run -d --name emissor-db -p 5433:5432 -e POSTGRES_PASSWORD=postgres postgres:16
docker exec emissor-db psql -U postgres -c 'create database estoque;'
docker exec emissor-db psql -U postgres -c 'create database faturamento;'

# Migrations
dotnet ef database update --project backend/Estoque.Api
dotnet ef database update --project backend/Faturamento.Api

# Serviços, em dois terminais
dotnet run --project backend/Estoque.Api        # http://localhost:5001/swagger
dotnet run --project backend/Faturamento.Api    # http://localhost:5002/swagger
```

## Testes

```bash
dotnet test Emissor.sln
```

Cobertura é coletada com **coverlet** e transformada em relatório pelo **ReportGenerator**;
o CI publica o resumo no sumário da execução a cada push e pull request.

---

Documentação de apoio: [`NOTES.md`](NOTES.md) traz o escopo e o desenho da solução;
[`CLAUDE.md`](CLAUDE.md), as convenções de trabalho no repositório.
