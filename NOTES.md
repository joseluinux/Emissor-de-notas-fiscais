# Emissor de Notas Fiscais — scope and design

> **Reading order for AI agents:** Part 1 is the scope — what the system must do, and the
> constraints it was built under. Part 2 is the working context: stack, layout, conventions
> and current status, derived from this repository.

---

# Part 1 — Scope

## Goal

An invoice issuance system built as a portfolio project, exercising a microservices
architecture end to end: two independent backend services with their own databases, an
and one operation that has to stay correct across a network boundary.

## Features

**Product registration**

Fields: code, description (product name), balance (quantity available in stock).

A product is registered up front so it can be used later in invoices.

**Invoice registration**

Fields: sequential numbering, status (Open or Closed), and multiple products with their
respective quantities.

An invoice is created with sequential numbering and initial status Open.

**Invoice printing**

A visible, intuitive print button on screen. Clicking it:

- shows a processing indicator while the request is in flight;
- on completion, updates the invoice status to Closed;
- refuses to print invoices whose status is not Open;
- updates each product's balance by the quantity used in the invoice.
  - Worked example: previous balance 10, invoice uses 2 units → new balance 8.

## Requirements

1. **Microservices architecture** — at least two services:
   - Stock service, controlling products and balances;
   - Billing service, managing invoices.
2. **Fault handling** — the system must survive one of the services failing, recover, and give
   the user meaningful feedback about the error.
3. **Real database** — records are physically persisted, not held in memory.

## Stretch goals

a. **Concurrency** — a product with balance 1 being consumed simultaneously by two invoices.
b. **AI** — some functionality backed by an AI model.
c. **Idempotency** — repeated operations must not cause unwanted side effects.

---

# Part 2 — Working context for agents

## What we are building, in one paragraph

Two independent ASP.NET Core services. **Estoque.Api** (Stock)
owns products and their balances. **Faturamento.Api** (Billing) owns invoices and their
items. The whole system hinges on one cross-service transaction: *printing* an invoice
flips it from `Open` to `Closed` **and** debits every item's quantity from stock — so
Billing must call Stock, and must behave correctly when Stock is down, when the balance
is insufficient, and when the same print is requested twice.

## Stack — decided (evidence in repo)

| Layer | Choice | Where it is pinned |
| --- | --- | --- |
| Backend framework | ASP.NET Core, **.NET 9** (`net9.0`) | both `*.csproj`, `global.json` |
| ORM | **EF Core 9** | both `*.csproj` |
| Database | **PostgreSQL** via `Npgsql.EntityFrameworkCore.PostgreSQL` 9.0.x | both `*.csproj` |
| API docs | **Swashbuckle.AspNetCore** 10.2.3 (Swagger UI in Development only) | `Program.cs` |
| API style | Controllers (`AddControllers()` / `MapControllers()`), not minimal APIs | `Program.cs` |
| Tests | **xUnit** + EF Core InMemory, **coverlet** + **ReportGenerator** for coverage | `*.Tests.csproj` |
| CI | **GitHub Actions** — build + test on every push and PR | `.github/workflows/ci.yml` |
| Frontend | **out of scope** — this repository is the backend | — |

Local toolchain verified on this machine: `dotnet` SDK 9.0.120, Node 26.7.0, npm 12.0.2,
Docker 29.7.2. `ng` and `psql` are **not** on PATH — use `npx ng` and run Postgres in a
container.

## Repository layout

```
.
├── README.md
├── NOTES.md                     ← this file
├── CLAUDE.md                    ← working conventions, loaded every session
├── Emissor.sln                  ← both backend projects and both test projects
├── global.json                  ← pins the SDK so CI and local agree
├── .github/workflows/ci.yml
├── backend/
│   ├── Estoque.Api/             ← Stock Service      · http://localhost:5001
│   ├── Estoque.Api.Tests/
│   ├── Faturamento.Api/         ← Billing Service    · http://localhost:5002
│   └── Faturamento.Api.Tests/
```

Ports come from each project's `Properties/launchSettings.json` (`http` profile,
`ASPNETCORE_ENVIRONMENT=Development`). Portuguese service names are the established
convention: *Estoque* = Stock, *Faturamento* = Billing.

## Current status — what actually exists

**The backend is complete except for fault handling.** Both services are built, tested, and
the link between them works: printing an invoice debits stock across HTTP and closes the note
in one operation.

Done:

- [x] `Emissor.sln` at the repo root, four projects; `CLAUDE.md` with the working conventions
- [x] **Estoque.Api**: `Produto` (+ `xmin` concurrency token), `MovimentacaoEstoque`/`...Item`,
      `EstoqueDbContext`, migration `Inicial` applied to the `estoque` database
- [x] **Estoque.Api**: `GET|POST /api/produtos`, `GET|PUT /api/produtos/{id}`,
      `POST /api/estoque/movimentacoes` (all-or-nothing debit, idempotent on `referencia`),
      `GET /api/estoque/movimentacoes/{referencia}`
- [x] **Estoque.Api**: ProblemDetails + global `IExceptionHandler` (domain refusals → 4xx,
      anything else → 500 with a correlation id)
- [x] Stretch (a) concurrency and (c) idempotency, both demonstrated under parallel load
- [x] **Faturamento.Api**: `NotaFiscal`/`NotaFiscalItem`, `AppDbContext` with the `Numero`
      sequence, migration applied, `GET|POST /api/notas`, `GET /api/notas/{id}`,
      ProblemDetails + `DominioExceptionHandler`
- [x] **The link**: typed `IEstoqueClient`/`EstoqueClient` via `AddHttpClient`, base address
      from `Servicos:Estoque:BaseUrl`, and `POST /api/notas/{id}/imprimir` — 404 / 409
      (nota nao Aberta) / 422 and 409 forwarded from Estoque with detail intact
- [x] Both `.http` files rewritten as the demo script; `Faturamento.Api.http` is the
      cross-service walkthrough
- [x] **Tests**: 84 xUnit tests (46 Estoque, 38 Faturamento), ~88% line coverage, coverage
      report via coverlet + ReportGenerator. Two lanes on purpose: the EF InMemory provider
      carries the pure-logic tests (`TestDb` in each project), and a real PostgreSQL lane
      carries what only a real database can prove
- [x] **Testcontainers lane**: `PostgresFixture` starts a disposable `postgres:16` once per
      run and applies the real migrations instead of `EnsureCreated`, so the suite also
      verifies that the migrations apply cleanly. `PostgresCollection` shares that container
      and forces sequential execution, which is what makes `TRUNCATE` between tests safe.
      `CriarContexto()` hands out a **new** context per call, so an assertion reads the
      database and not the change tracker
- [x] **CI**: GitHub Actions running build + tests on every push and pull request
- [x] **Docs**: `docs/mecanismos.md` maps the three guarantees that do not live in any single
      file — idempotency, concurrency and failure ordering — with the pieces by
      `file:line`, what breaks if each is removed, and a live demo for each

Known weakness in the test suite — half fixed:

- The InMemory provider does not enforce unique indexes, has no `xmin`, and has no real
  transactions, so the three hardest guarantees passed there by accident. Measured, not
  assumed: deleting `.IsUnique()` from `Referencia` left all 81 tests green, back when the
  suite was InMemory only.
- [x] **Idempotency** is now verified on the Postgres lane — a repeated `referencia` replays
      instead of debiting twice, and the unique index rejects the duplicate row.
- [ ] **Concurrency** (`xmin` → 409) and the **all-or-nothing** debit still have no
      real-database test. Same lane, next ticket.
- Some InMemory tests still assert against the same `DbContext` used to act, so "it was
  persisted" passes even when nothing is saved. The Postgres lane avoids this by
  construction; the InMemory tests have not been swept.

- [x] **Requirement 2 — fault handling**: the Estoque client carries timeout, retry and a
      circuit breaker. With Estoque down, printing answers **503 in ~10s with a readable
      message** instead of 500 in 100s with a stack trace, and the invoice stays Aberta

Still missing:

- [ ] No `docker-compose.yml` for PostgreSQL (decided against for now)
- [ ] Stretch goal (b), AI, not attempted — out of scope by decision

## Design

### Domain model

Follow the Portuguese naming already set by the project names.

**Estoque.Api** — `Produto`
- `Id` (PK), `Codigo` (unique, the business key), `Descricao`, `Saldo` (int, ≥ 0)
- Concurrency token — `xmin` mapped as a row version is the idiomatic Npgsql choice, and it
  is what makes stretch goal (a) demonstrable

**Faturamento.Api** — `NotaFiscal`
- `Id` (PK), `Numero` (sequential, unique), `Status` (`Aberta` | `Fechada`),
  `CriadaEm`, `ImpressaEm?`
- `Itens`: collection of `NotaFiscalItem` → `ProdutoCodigo`, `Descricao` (snapshot),
  `Quantidade`
- Sequential numbering comes from a Postgres sequence, never `MAX(Numero)+1` in application
  code.

Each service owns its own schema. Billing stores a product reference plus a snapshot of
the description; it never reads Stock's tables directly, only its HTTP API.

### Endpoints

Stock — `:5001`
- `GET /api/produtos`, `GET /api/produtos/{id}`, `POST /api/produtos`, `PUT /api/produtos/{id}`
- `POST /api/estoque/movimentacoes` — the debit; all-or-nothing across every line, and the
  single most important endpoint in the system. Products are addressed by **`produtoCodigo`**,
  never by Stock's `Id`.

  ```jsonc
  // request
  { "referencia": "nota-42", "itens": [ { "produtoCodigo": "P001", "quantidade": 2 } ] }

  // 201 Created — debited
  // 200 OK      — replay of an earlier call with this referencia; nothing debited
  { "id": 1, "referencia": "nota-42", "criadaEm": "...",
    "itens": [ { "produtoCodigo": "P001", "quantidade": 2, "saldoResultante": 8 } ] }

  // 422 — saldo insuficiente (lists every shortfall) or produto não cadastrado
  // 409 — concurrency conflict; the caller should retry
  ```

  `referencia` is the idempotency key and carries a unique index. Reusing it is safe and
  returns a byte-identical body; there is no `Idempotency-Key` header.
- `GET /api/estoque/movimentacoes/{referencia}` — audit trail for one movement

Billing — `:5002`
- `GET /api/notas`, `GET /api/notas/{id}`
- `POST /api/notas` — creates with next `Numero` and status `Aberta`
- `POST /api/notas/{id}/imprimir` — the print flow below; calls Stock's
  `POST /api/estoque/movimentacoes` with `referencia = "nota-{id}"`

### The print flow (the core scenario)

1. Reject unless `Status == Aberta` → **409 Conflict**.
2. Call Stock's debit endpoint with all items in one request.
3. On success → set `Status = Fechada`, stamp `ImpressaEm`, return the note.
4. On insufficient balance → **422**, invoice stays `Aberta`, message names the product.
5. On Stock unreachable → **503**, invoice stays `Aberta`, user sees a clear "Stock
   service unavailable, try again" message. **This is requirement 2** — the demo is: stop
   `Estoque.Api`, click Print, show the friendly error, restart it, click Print again, show
   it succeed.

Keep the invoice status change and the stock debit in that order (debit first, close
after) so a failure never leaves a closed invoice with un-debited stock.

### Requirement 2 — fault handling

The Billing→Stock typed client carries a standard resilience handler: 2s per attempt, 2
retries, a circuit breaker over a 10s window, and a 10s ceiling for the whole operation. The
2s attempt timeout is what replaces the `HttpClient` default of **100 seconds**.

Transport failures, timeouts and an open circuit are translated in `EstoqueClient` into
`EstoqueIndisponivelException` → **503**, with the original exception kept as `InnerException`
so the log distinguishes a refused connection from a timeout. A cancellation requested by the
caller is explicitly *not* treated as unavailability — that would blame Estoque for a user
closing the tab.

Retrying a stock debit is only safe because the operation is idempotent on `referencia`: if
the first attempt reached Estoque and the response was lost, the second finds `nota-{id}`
already recorded and replays it. Without that guarantee the correct policy would be no retry
at all.

### Backend error handling

- A global exception handler (`IExceptionHandler` + `AddExceptionHandler`), mapping
  exceptions to **ProblemDetails** (RFC 7807).
- Domain exceptions (`SaldoInsuficienteException`, `NotaNaoAbertaException`) → 4xx with a
  readable `detail`; anything unexpected → 500 with a correlation id, logged server-side.
- Validation via DataAnnotations / model state → 400.
- Never leak stack traces outside Development.

### LINQ

Used visibly and idiomatically in EF Core queries: projections to DTOs with `.Select()`,
filtering with `.Where()`, `.AnyAsync()` for existence checks, `.Include()` for invoice
items, `.OrderBy(n => n.Numero)`. LINQ here is translated to SQL by EF Core rather than
executed in memory.

### Stretch goals

- **(a) Concurrency:** the `xmin` row-version on `Produto` → `DbUpdateConcurrencyException`
  on simultaneous debits → one request wins, the other gets a clean 409. Demo with two
  parallel print requests against a product with balance 1.
- **(c) Idempotency:** the unique index on `Referencia`; a repeated debit replays the stored
  result instead of debiting twice.
- **(b) AI:** optional; if attempted, keep it small and clearly scoped (e.g. generating a
  product description from its code). Do not let it delay the mandatory scope.

## Local development

```bash
# PostgreSQL — runs as the container `emissor-db`, on host port 5433.
docker run -d --name emissor-db -p 5433:5432 -e POSTGRES_PASSWORD=postgres postgres:16
docker exec emissor-db psql -U postgres -c 'create database estoque;'
docker exec emissor-db psql -U postgres -c 'create database faturamento;'

# Backend — two terminals
dotnet run --project backend/Estoque.Api        # http://localhost:5001  /swagger
dotnet run --project backend/Faturamento.Api    # http://localhost:5002  /swagger

# Tests
dotnet test Emissor.sln

# EF Core migrations (per service)
dotnet ef migrations add Inicial --project backend/Estoque.Api
dotnet ef database update --project backend/Estoque.Api
```

The local dev connection strings live in each service's committed
`appsettings.Development.json` (`Host=localhost;Port=5433`, user/password `postgres`). There is
no real secret there and a reviewer should be able to clone and `dotnet run`; use user-secrets
if that ever stops being true. `bin/` and `obj/` are **not** tracked.

## Open decisions

1. Whether to attempt the AI stretch goal, and with what provider.
2. Whether services get a `docker-compose.yml` (nice for the demo) or stay `dotnet run`.

## Demo walkthrough checklist

A recorded walkthrough is worth having for the portfolio, and preparing it is the best test
of whether the code is actually understood. Keep it answerable at all times:

- [ ] Demo: register product → create invoice → print → show balance decreased
- [ ] Demo: print an already-closed invoice → blocked
- [ ] Demo: same `referencia` twice → debited once, identical body both times
- [ ] Demo: two prints racing for the last unit → one 200, one 409, balance never negative
- [ ] Demo: Stock service down → 503 with a readable message → restart → succeeds
- [ ] Explain: backend framework choice (ASP.NET Core 9 + EF Core)
- [ ] Explain: error/exception handling strategy and why every failure is ProblemDetails
- [ ] Explain: where and how LINQ is used
- [ ] Explain: why retry is safe here, and why it would not be without idempotency
- [ ] Explain: the two test lanes, and what the InMemory one cannot prove
