# Emissor de Notas Fiscais

[![CI](https://github.com/joseluinux/Emissor-de-notas-fiscais/actions/workflows/ci.yml/badge.svg)](https://github.com/joseluinux/Emissor-de-notas-fiscais/actions/workflows/ci.yml)

Backend de um sistema de emissão de notas fiscais, em arquitetura de microsserviços: dois
serviços **ASP.NET Core 9** independentes, cada um dono do seu próprio banco **PostgreSQL**,
conversando apenas por HTTP.

| Serviço | Responsabilidade | Porta |
| --- | --- | --- |
| **Estoque** | Produtos e saldos | `:5001` |
| **Faturamento** | Notas fiscais e impressão | `:5002` |

O núcleo do sistema é uma operação que atravessa os dois: **imprimir** uma nota fecha o
documento **e** debita do estoque a quantidade de cada item. Como não existe transação
distribuída entre dois bancos separados por rede, essa operação precisa se comportar
corretamente quando o saldo é insuficiente, quando a mesma impressão chega duas vezes, quando
duas notas disputam a última unidade, e quando o Estoque está fora do ar.

## Decisões técnicas

**Idempotência.** A baixa de estoque é ancorada num índice único de `Referencia`. Reimprimir
replica o resultado original em vez de debitar de novo. A consulta prévia é otimização; a
garantia é a restrição no banco — sob concorrência as duas requisições passam pela consulta
sem ver uma a outra, e é o PostgreSQL que rejeita a segunda gravação.

**Concorrência.** `Produto` usa a coluna de sistema `xmin` do PostgreSQL como token de
concorrência otimista. Duas baixas simultâneas sobre o mesmo saldo geram um 409 limpo para a
perdedora, e o saldo nunca fica negativo.

**Ordem de falha.** Debitar primeiro, fechar depois. Sem atomicidade entre os dois serviços, a
escolha não é *como evitar inconsistência* e sim *qual inconsistência preferir*: estoque
subestimado é erro de conciliação e se recupera sozinho na próxima tentativa; estoque
superestimado é vender o que não existe, e exige intervenção manual.

**Resiliência.** O cliente do Estoque tem timeout, retry e circuit breaker. Com o Estoque fora
do ar, a impressão responde **503 em ~10s com instrução do que fazer**, em vez de 500 em 100s
com stack trace, e a nota continua Aberta. O retry só é seguro porque a baixa é idempotente —
sem essa garantia, uma resposta perdida viraria débito duplo.

**Erros.** Toda falha sai como RFC 7807 ProblemDetails, produzida por um `IExceptionHandler`
global. Recusas de negócio viram 4xx com mensagem legível; o inesperado vira 500 com
`correlationId` que aparece no log do servidor. Nenhum controller monta corpo de erro.

## Testes

**93 testes em duas pistas**, por decisão explícita:

| Pista | Cobre | Por quê |
| --- | --- | --- |
| EF InMemory | lógica pura — agrupamento, validação, projeção, mapeamento de erro | rápida |
| **PostgreSQL real** via Testcontainers | idempotência, concorrência, atomicidade | o provider InMemory não aplica índice único, não tem `xmin` e não tem transação |

A segunda pista existe por um motivo medido: com 81 testes verdes e 88% de cobertura, apagar
o `.IsUnique()` da `Referencia` **não fazia nenhum teste falhar**. Cobertura mede linha
executada, não comportamento verificado.

O CI tem um passo que lê os resultados e **quebra a build se nenhum teste da pista PostgreSQL
executar** — um passo verde não diz quais testes rodaram.

```bash
dotnet test Emissor.sln
```

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

Os arquivos `.http` de cada projeto são o roteiro de demonstração, incluindo o fluxo que
atravessa os dois serviços.

## Escopo

Este repositório é **o backend**. Não há front-end: a interface está fora do escopo por
decisão, não por estar pendente.

---

[`docs/mecanismos.md`](docs/mecanismos.md) detalha as três garantias que não moram em nenhum
arquivo — idempotência, concorrência e ordem de falha — com as peças por `arquivo:linha`, o que
quebra se cada uma for removida, e como demonstrar cada uma ao vivo.
