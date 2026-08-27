# Korp — Invoice Issuance System (technical test)

> **Reading order for AI agents:** Part 1 is the client brief (the contract — do not
> reinterpret it). Part 2 is the working context: stack, layout, conventions and current
> status, derived from this repository. When they disagree, Part 1 wins.

---

# Part 1 — Original brief (verbatim)

Technical project: Invoice issuance system

## Objective

Develop an application in Angular, according to the requirements described below, and
present the results in video format, demonstrating:

- The screens developed;
- The implemented features;
- A technical detail of the solution.

In the technical details, inform:

- Which Angular lifecycles were used;
- Whether use of the RxJS library was made and, if so, how;
- Which other libraries were used and for what purpose;
- For visual components, which libraries were used;
- How dependency management was carried out in Golang (if applicable);
- Which frameworks were used in Golang or C#;
- How errors and exceptions were handled in the backend;
- If the implementation uses C#, indicate whether LINQ was used and how.

## Scope

### 1. Features to be developed

**Product Registration**

Mandatory fields:

- Code
- Description (product name)
- Balance (quantity available in stock)

Expected result: allow a product to be previously registered for later use in invoices.

**Tax Invoice Registration**

Mandatory fields:

- Sequential numbering
- Status: Open or Closed
- Inclusion of multiple products with respective quantities

Expected result: allow the creation of an invoice with sequential numbering and initial
status Open.

**Printing of Invoices**

- Visible and intuitive print button on screen.

Expected result:

- When clicking the button, display processing indicator;
- After completion, update the note status to Closed;
- Do not allow printing of notes with different status Open;
- Update product balance according to quantity used in the note.
  - Example: previous balance = 10; note uses 2 units → new balance = 8.

### Mandatory requirements

1. **Microservices Architecture:** Structure the system with at least two microservices:
   - Stock Service – control of products and balances;
   - Billing Service – management of invoices.
2. **Fault Handling:** Implement a scenario where one of the microservices fails. The
   system must be able to recover from the failure and provide appropriate feedback to
   the user about the error.
3. **Real Connection to Database:** It is expected that records are physically persisted
   in a database of your choice.

### Optional requirements

The candidate may, at his/her discretion, also implement:

a. **Competition Treatment:** Scenario: product with balance 1 being used simultaneously
   by two bills.
b. **Use of Artificial Intelligence:** Implement some system functionality that uses AI.
c. **Idempotence Implementation:** Ensure that repeated operations do not cause unwanted
   side effects.

---

# Part 2 — Working context for agents

## What we are building, in one paragraph

An Angular SPA talking to two independent ASP.NET Core services. **Estoque.Api** (Stock)
owns products and their balances. **Faturamento.Api** (Billing) owns invoices and their
items. The whole test hinges on one cross-service transaction: *printing* an invoice
flips it from `Open` to `Closed` **and** debits every item's quantity from stock — so
Billing must call Stock, and must behave correctly when Stock is down, when the balance
is insufficient, and when the same print is requested twice.

## Stack — decided (evidence in repo)

| Layer | Choice | Where it is pinned |
| --- | --- | --- |
| Backend framework | ASP.NET Core, **.NET 9** (`net9.0`) | both `*.csproj` |
| ORM | **EF Core 9** (`Microsoft.EntityFrameworkCore.Design` 9.0.19) | both `*.csproj` |
| Database | **PostgreSQL** via `Npgsql.EntityFrameworkCore.PostgreSQL` 9.0.4 | both `*.csproj` |
| API docs | **Swashbuckle.AspNetCore** 10.2.3 (Swagger UI in Development only) | `Program.cs` |
| API style | Controllers (`AddControllers()` / `MapControllers()`), not minimal APIs | `Program.cs` |
| Frontend | **Angular** (required by the brief) | `frontend/` — not scaffolded yet |

Local toolchain verified on this machine: `dotnet` SDK 9.0.120, Node 26.7.0, npm 12.0.2,
Docker 29.7.2. `ng` and `psql` are **not** on PATH — use `npx ng` and run Postgres in a
container (or via a client of your choice).

Because the backend is C#, two brief questions are answered by construction: the Golang
dependency-management question is **not applicable**, and the LINQ question **must** be
answered — so write queries with LINQ deliberately and be able to point at them.

## Repository layout

```
.
├── NOTES.md                     ← this file
├── README.md                    ← placeholder, still just the repo name
├── backend/
│   ├── Estoque.Api/             ← Stock Service      · http://localhost:5001
│   └── Faturamento.Api/         ← Billing Service    · http://localhost:5002
└── frontend/                    ← EMPTY. Angular app goes here (default: :4200)
```

Ports come from each project's `Properties/launchSettings.json` (`http` profile,
`ASPNETCORE_ENVIRONMENT=Development`). There is **no `.sln`** — projects are run
individually with `dotnet run --project <path>`. Portuguese service names are the
established convention: *Estoque* = Stock, *Faturamento* = Billing.

## Current status — what actually exists

**The backend is complete for day 1.** Both services are built and the link between them
works: printing an invoice debits stock across HTTP and closes the note in one operation.

Done:

- [x] `Korp.sln` at the repo root, both projects; `CLAUDE.md` with the working conventions
- [x] **Estoque.Api**: `Produto` (+ `xmin` concurrency token), `MovimentacaoEstoque`/`...Item`,
      `EstoqueDbContext`, migration `Inicial` applied to the `estoque` database
- [x] **Estoque.Api**: `GET|POST /api/produtos`, `GET|PUT /api/produtos/{id}`,
      `POST /api/estoque/movimentacoes` (all-or-nothing debit, idempotent on `referencia`),
      `GET /api/estoque/movimentacoes/{referencia}`
- [x] **Estoque.Api**: ProblemDetails + global `IExceptionHandler` (domain refusals → 4xx,
      anything else → 500 with a correlation id)
- [x] Optional (a) concurrency and (c) idempotency, both demonstrated under parallel load
- [x] **Faturamento.Api** (Ticket 2): `NotaFiscal`/`NotaFiscalItem`, `AppDbContext` with the
      `Numero` sequence, migration applied, `GET|POST /api/notas`, `GET /api/notas/{id}`,
      ProblemDetails + `DominioExceptionHandler`
- [x] **The link** (Ticket 3): typed `IEstoqueClient`/`EstoqueClient` via `AddHttpClient`,
      base address from `Servicos:Estoque:BaseUrl`, and `POST /api/notas/{id}/imprimir` —
      404 / 409 (nota nao Aberta) / 422 and 409 forwarded from Estoque with detail intact
- [x] Both `.http` files rewritten as the demo script; `Faturamento.Api.http` is the
      cross-service walkthrough

Still missing:

- [ ] No resilience policy, and **no 503 path when Estoque is unreachable** — deliberately
      deferred; today a failed call surfaces as a 500. This is mandatory requirement 2.
- [ ] No CORS policy — required before Angular on :4200 can call :5001/:5002
- [ ] No Angular workspace in `frontend/`
- [ ] No `docker-compose.yml` for PostgreSQL (decided against for now)

Treat everything in "Proposed design" below as the default to build unless the user says
otherwise; it is derived from the brief, not dictated by it.

## Proposed design

### Domain model

Follow the Portuguese naming already set by the project names.

**Estoque.Api** — `Produto`
- `Id` (PK), `Codigo` (unique, the brief's "Code"), `Descricao`, `Saldo` (int, ≥ 0)
- Concurrency token (`xmin` as a `[Timestamp]`/`IsRowVersion` property is the idiomatic
  Npgsql choice) — this is what makes optional requirement (a) demonstrable

**Faturamento.Api** — `NotaFiscal`
- `Id` (PK), `Numero` (sequential, unique), `Status` (`Aberta` | `Fechada`),
  `CriadaEm`, `ImpressaEm?`
- `Itens`: collection of `NotaFiscalItem` → `ProdutoId`/`ProdutoCodigo`, `Descricao`
  (snapshot), `Quantidade`
- Sequential numbering: use a Postgres sequence or `MAX(Numero)+1` **inside** the
  insert transaction — never compute it in the client.

Each service owns its own schema. Billing stores a product reference plus a snapshot of
the description; it never reads Stock's tables directly, only its HTTP API.

### Endpoints (first cut)

Stock — `:5001` — **BUILT, this is the real contract**
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

Billing — `:5002` — **not built yet**
- `GET /api/notas`, `GET /api/notas/{id}`
- `POST /api/notas` — creates with next `Numero` and status `Aberta`
- `POST /api/notas/{id}/imprimir` — the print flow below; calls Stock's
  `POST /api/estoque/movimentacoes` with `referencia = "nota-{id}"`

### The print flow (the core scenario)

1. Reject unless `Status == Aberta` → **409 Conflict** ("Do not allow printing of notes
   with different status Open").
2. Call Stock's debit endpoint with all items in one request.
3. On success → set `Status = Fechada`, stamp `ImpressaEm`, return the note.
4. On insufficient balance → **422**, invoice stays `Aberta`, message names the product.
5. On Stock unreachable → **503**, invoice stays `Aberta`, user sees a clear "Stock
   service unavailable, try again" message. **This is mandatory requirement 2** — the
   demo is: stop `Estoque.Api`, click Print, show the friendly error, restart it, click
   Print again, show it succeed.

Keep the invoice status change and the stock debit in that order (debit first, close
after) so a failure never leaves a closed invoice with un-debited stock.

### Mandatory requirement 2 — fault handling

Register the Billing→Stock client with `IHttpClientFactory` and a short timeout, and
map the failure to a user-facing message. `Microsoft.Extensions.Http.Resilience` (or
Polly) gives retry + circuit breaker in a few lines and is worth mentioning in the
video. The user must never see a raw 500 or a stack trace.

### Backend error handling (a brief question — be ready to explain it)

- A global exception handler (`IExceptionHandler` + `AddExceptionHandler`, .NET 8+) or
  `UseExceptionHandler` middleware, mapping exceptions to **ProblemDetails** (RFC 7807).
- Domain exceptions (`SaldoInsuficienteException`, `NotaNaoAbertaException`) → 4xx with a
  readable `detail`; anything unexpected → 500 with a correlation id, logged server-side.
- Validation via DataAnnotations / model state → 400.
- Never leak stack traces outside Development.

### LINQ (a brief question — be ready to explain it)

Use LINQ visibly and idiomatically in EF Core queries: projections to DTOs with
`.Select()`, filtering with `.Where()`, `.AnyAsync()` for existence checks, `.Include()`
for invoice items, `.OrderBy(n => n.Numero)`. Prepare one concrete example to show on
screen; mention that LINQ here is translated to SQL by EF Core rather than executed
in memory.

### Optional requirements

- **(a) Concurrency:** the `xmin` row-version on `Produto` → `DbUpdateConcurrencyException`
  on simultaneous debits → one request wins, the other gets a clean 409. Demo with two
  parallel print requests against a product with balance 1.
- **(c) Idempotency:** accept an `Idempotency-Key` header on print/debit, store handled
  keys with their response, and replay it on repeat. Cheaper alternative that still
  satisfies the requirement: printing an already-`Fechada` note returns the same result
  instead of debiting twice.
- **(b) AI:** optional; if attempted, keep it small and clearly scoped (e.g. generating a
  product description from its code). Do not let it delay the mandatory scope.

### Frontend conventions

The video must answer *which lifecycles* and *how RxJS was used*, so build in a way that
gives real answers:

- Screens: product list + form, invoice list + form (multi-item), invoice detail with the
  **Print** button and its processing indicator (disable the button while in flight).
- Lifecycles: `ngOnInit` for initial loads, `ngOnDestroy` for subscription teardown,
  `ngOnChanges` where a child component reacts to input changes.
- RxJS: `HttpClient` observables, `switchMap` for dependent calls, `catchError` mapping
  backend ProblemDetails to UI messages, `finalize` to clear the loading flag,
  `takeUntilDestroyed`/`takeUntil` for unsubscription, `debounceTime` on any search.
- Component library: pick **one** and stick to it (Angular Material or PrimeNG) — the
  brief asks explicitly which visual library was used.
- Point the app at the two services through environment files, not hardcoded URLs.

## Local development

```bash
# PostgreSQL — ALREADY RUNNING as the container `korp-db`, on host port 5433.
# Databases `estoque` and `faturamento` already exist. Only recreate if the container is gone:
docker run -d --name korp-db -p 5433:5432 -e POSTGRES_PASSWORD=postgres postgres:16
docker exec korp-db psql -U postgres -c 'create database estoque;'
docker exec korp-db psql -U postgres -c 'create database faturamento;'

# Backend — two terminals
dotnet run --project backend/Estoque.Api        # http://localhost:5001  /swagger
dotnet run --project backend/Faturamento.Api    # http://localhost:5002  /swagger

# EF Core migrations (per service)
dotnet ef migrations add Inicial --project backend/Estoque.Api
dotnet ef database update --project backend/Estoque.Api

# Frontend (ng is not installed globally)
npx ng serve                                     # http://localhost:4200
```

The local dev connection strings live in each service's committed
`appsettings.Development.json` (`Host=localhost;Port=5433`, user/password `postgres`). There is
no real secret there and a reviewer should be able to clone and `dotnet run`; use user-secrets
if that ever stops being true. `bin/` and `obj/` are **not** tracked — `.gitignore` already
covers them.

## Open decisions (need a human call)

1. Angular version and component library (Material vs PrimeNG) — affects every screen.
2. Whether to attempt the optional AI requirement, and with what provider.
3. Whether services get a `docker-compose.yml` (nice for the demo) or stay `dotnet run`.
4. UI language — Portuguese labels (matching the backend naming) or English.

## Video deliverable checklist

The recording is part of the deliverable, so keep it answerable at all times:

- [ ] Walk through every screen
- [ ] Demo: register product → create invoice → print → show balance decreased
- [ ] Demo: print an already-closed invoice → blocked
- [ ] Demo: Stock service down → friendly error → recovery
- [ ] Explain: Angular lifecycles used
- [ ] Explain: RxJS operators used and why
- [ ] Explain: other libraries + the visual component library
- [ ] Explain: C# framework (ASP.NET Core 9 + EF Core), Golang N/A
- [ ] Explain: backend error/exception handling strategy
- [ ] Explain: where and how LINQ is used
