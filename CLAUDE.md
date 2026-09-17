# Emissor de Notas Fiscais — working notes for agents

Invoice issuance system, built as a portfolio project. Read `NOTES.md` for the scope and the
full design; this file holds only the conventions that must survive every session.

## Ticket batching

Work **2–5 tickets at a time, 3 by default**. Go above 3 only when the three would total under
roughly 500 lines of logic. Do not plan a backlog — plan the next batch, finish it, then
re-batch. A ticket is a vertical slice that can be built and verified on its own, not a
checklist item.

## Layout

```
backend/Estoque.Api/       Stock service    — produtos, saldos    :5001
backend/Faturamento.Api/   Billing service  — notas fiscais       :5002
Emissor.sln                both backend projects
```

Portuguese domain naming throughout: *Estoque* = Stock, *Faturamento* = Billing,
*Produto*, *NotaFiscal*, *Saldo*, *Movimentação*. Keep it.

Each service owns its own database and schema. Faturamento never reads Estoque's tables —
only its HTTP API — and stores a `ProdutoCodigo` (the business key) plus a snapshot of the
description, never a foreign key across services.

## Local environment

PostgreSQL runs in the container `emissor-db` (postgres:16), **host port 5433**, user/password
`postgres`/`postgres`, with databases `estoque` and `faturamento` already created.

```bash
dotnet build Emissor.sln
dotnet run --project backend/Estoque.Api        # http://localhost:5001/swagger
dotnet run --project backend/Faturamento.Api    # http://localhost:5002/swagger

dotnet ef migrations add <Name> --project backend/Estoque.Api
dotnet ef database update       --project backend/Estoque.Api

docker exec emissor-db psql -U postgres -d estoque -c 'select * from "Produtos";'

# Timestamps print as UTC (the container's timezone). To read them in Brasília time:
docker exec emissor-db psql -U postgres -d estoque \
  -c "set timezone='America/Sao_Paulo';" -c 'select * from "MovimentacoesEstoque";'
```

Table names are EF-default PascalCase, so `psql` queries need double quotes.

`ng` and `psql` are not on PATH — use `npx ng` and `docker exec emissor-db psql`.

## Conventions

- **Errors:** every failure leaves the API as RFC 7807 ProblemDetails, produced by the global
  `IExceptionHandler`. Controllers throw domain exceptions; they do not build error bodies.
- **LINQ:** write queries as visible, idiomatic LINQ — the demo walkthrough needs concrete
  examples to point at, and the explanation that EF Core translates them to SQL.
- **Migrations:** never hand-edit a generated migration; add a new one.
- **Timestamps:** always `DateTime.UtcNow` into `timestamptz`, serialized as ISO 8601 with the
  `Z` suffix. Never `DateTime.Now`, never store local time. A row reading `23:59+00` is
  `20:59` in Brasília — the `+00` says so. Converting to the user's timezone is the *display*
  layer's job: `set timezone` in psql, or the client's formatter in any UI.
