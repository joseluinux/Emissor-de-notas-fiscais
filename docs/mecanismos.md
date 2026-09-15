# Mecanismos

Três garantias deste sistema **não moram em nenhum arquivo**: elas emergem da relação entre
vários. Por isso comentário de linha não consegue explicá-las — um comentário responde
*"por que esta linha é estranha?"*, nunca *"como este sistema funciona?"*.

Este documento é o mapa. Cada seção traz o problema, as peças com `arquivo:linha`, o que
quebra se você remover cada uma, e como demonstrar ao vivo.

> As linhas citadas valem para o commit em que este arquivo foi escrito. Se não baterem,
> procure pelo trecho de código citado — ele é a referência real, não o número.

| Mecanismo | Resolve | Arquivos envolvidos |
| --- | --- | --- |
| [Idempotência](#1-idempotência) | a mesma operação pedida duas vezes | 3 |
| [Concorrência](#2-concorrência) | duas operações simultâneas no mesmo saldo | 3 |
| [Ordem de falha](#3-ordem-de-falha) | falha no meio de uma operação entre serviços | 2 |

---

## 1. Idempotência

### O problema

Imprimir uma nota debita o estoque. Se a mesma impressão for pedida duas vezes — o usuário
clicou de novo, a rede repetiu a requisição, uma falha interrompeu o fluxo no meio — o
estoque **não pode** ser debitado duas vezes.

### A pergunta que define tudo

O estoque é debitado em **uma única linha do sistema inteiro**:

```csharp
// backend/Estoque.Api/Services/MovimentacaoEstoqueService.cs:101
produto.Saldo -= linha.Quantidade;
```

Entender a idempotência aqui é responder: **o que impede a linha 101 de rodar duas vezes
para a mesma referência?** A resposta são dois `return` que a pulam.

### As quatro peças

| # | Peça | Onde |
| --- | --- | --- |
| 1 | Chave determinística `"nota-{id}"` | `Faturamento.Api/Controllers/NotasController.cs:130` |
| 2 | Índice único em `Referencia` | `Estoque.Api/Data/EstoqueDbContext.cs:47` |
| 3 | Atalho de replay | `Estoque.Api/Services/MovimentacaoEstoqueService.cs:64` |
| 4 | Rede de proteção (violação 23505) | `Estoque.Api/Services/MovimentacaoEstoqueService.cs:133` |

**Peça 1 — a chave determinística.** A idempotência começa em quem chama, não em quem
recebe. O Faturamento monta `$"nota-{nota.Id}"`: a mesma nota gera sempre a mesma string.
O prefixo cria um espaço de nomes — `nota-1` não colide com um `ajuste-1` de inventário
manual. O Estoque não sabe o que é uma nota fiscal; para ele a string é opaca.

**Peça 2 — o índice único.** Vira `IX_MovimentacoesEstoque_Referencia UNIQUE` no PostgreSQL.
É **a única garantia real**. Confirme no banco:

```bash
docker exec emissor-db psql -U postgres -d estoque -c '\d "MovimentacoesEstoque"'
```

**Peça 3 — o atalho.** Uma consulta por referência. Se já existe, devolve o registro guardado
e nunca chega na linha 101. É **otimização**, não garantia.

**Peça 4 — a rede.** Se duas requisições passarem juntas pela peça 3, o Postgres rejeita a
segunda gravação com `SQLSTATE 23505`. O `catch` traduz a rejeição em replay em vez de deixar
virar erro 500.

### O fluxo

```
   RegistrarBaixaAsync("nota-13")
              │
              ▼
  :64  SELECT ... WHERE Referencia = 'nota-13'
              │
     ┌────────┴────────┐
   achou            não achou
     │                 │
  :67 return           ▼
  (replay, true)  :101  produto.Saldo -= 2      ← ÚNICO ponto de débito
   ── SAÍDA 1 ──  :118  SaveChanges
   nunca debita          │
                    ┌────┴────┐
                   ok      23505
                    │         │
                    │   :150 return (replay, true)
                    │    ── SAÍDA 2 ──
                    │    o débito desta requisição é descartado
                    ▼
              :160 return (resposta, false)
```

Para fora, isso aparece como **201** (debitou) ou **200** (replicou), decidido em
`MovimentacoesEstoqueController.cs:34`.

### O que quebra sem cada peça

| Remova | Segundo clique sequencial | Dois cliques simultâneos |
| --- | --- | --- |
| Peça 1 (chave fixa → GUID) | **debita de novo** | debita de novo |
| Peça 2 (`IsUnique`) | continua funcionando | **debita duas vezes** |
| Peça 3 (atalho) | funciona, via exceção | funciona |
| Peça 4 (catch 23505) | funciona | **erro 500 na cara do usuário** |

A linha mais importante desta tabela é a segunda: **o caso sequencial é salvo pela consulta;
o caso simultâneo é salvo pelo índice.** Verificação em código de aplicação nunca é garantia
sob concorrência — isso tem nome, *TOCTOU* (`time-of-check to time-of-use`).

### Como demonstrar

Reproduz o estado pós-falha (débito feito, nota ainda Aberta) e mostra que o segundo clique
não debita de novo:

```bash
COD="DEMO-$(date +%s)"

# produto com saldo 10
curl -s -X POST localhost:5001/api/produtos -H 'Content-Type: application/json' \
  -d "{\"codigo\":\"$COD\",\"descricao\":\"Teclado\",\"saldo\":10}"

# nota usando 2 unidades -> anote o id devolvido
curl -s -X POST localhost:5002/api/notas -H 'Content-Type: application/json' \
  -d "{\"itens\":[{\"produtoCodigo\":\"$COD\",\"descricao\":\"Teclado\",\"quantidade\":2}]}"

# SIMULA A FALHA: debita direto no Estoque com a referência que o Imprimir usaria,
# sem fechar a nota. Saldo vai para 8, nota continua Aberta.
curl -s -X POST localhost:5001/api/estoque/movimentacoes -H 'Content-Type: application/json' \
  -d "{\"referencia\":\"nota-<ID>\",\"itens\":[{\"produtoCodigo\":\"$COD\",\"quantidade\":2}]}"

# o usuário clica em Imprimir -> nota fecha, saldo CONTINUA 8
curl -s -X POST localhost:5002/api/notas/<ID>/imprimir

# prova: uma única baixa registrada
docker exec emissor-db psql -U postgres -d estoque \
  -c "select count(*) from \"MovimentacoesEstoque\" where \"Referencia\" = 'nota-<ID>';"
```

---

## 2. Concorrência

### O problema

Um produto com saldo 1 sendo consumido por duas notas ao mesmo tempo. Sem proteção, as duas
leem `Saldo = 1`, as duas validam, as duas gravam — e o saldo vai a `-1`.

### As peças

| # | Peça | Onde |
| --- | --- | --- |
| 1 | Propriedade `Version` | `Estoque.Api/Domain/Produto.cs:16` |
| 2 | Mapeamento para `xmin` | `Estoque.Api/Data/EstoqueDbContext.cs:36` |
| 3 | Captura do conflito | `Estoque.Api/Services/MovimentacaoEstoqueService.cs:120` |
| 4 | Tradução para 409 | `Estoque.Api/Errors/DomainException.cs:86` |

`xmin` é uma **coluna de sistema do PostgreSQL**: toda linha carrega o id da transação que a
escreveu por último, e o valor muda sozinho a cada `UPDATE`. Mapeada como *row version*, o
EF Core passa a incluí-la no `WHERE` de todo update:

```sql
UPDATE "Produtos" SET "Saldo" = 0 WHERE "Id" = 1 AND xmin = 12345;
```

Se outra transação alterou a linha nesse meio-tempo, o `xmin` mudou, o `WHERE` não casa,
**zero linhas são afetadas** e o EF lança `DbUpdateConcurrencyException`. Uma requisição
ganha, a outra recebe **409** com os códigos em conflito no corpo — e o saldo nunca fica
negativo.

Vantagem sobre uma coluna de versão própria: não exige coluna extra nem migration, o
PostgreSQL já mantém.

### Como demonstrar

Produto com saldo 1, duas notas, duas impressões disparadas em paralelo:

```bash
curl -s -X POST localhost:5002/api/notas/<ID_A>/imprimir &
curl -s -X POST localhost:5002/api/notas/<ID_B>/imprimir &
wait
```

Saída real de uma execução (reproduziu na primeira tentativa):

```
nota 15 -> HTTP 200
nota 14 -> HTTP 409
saldo = 0
```

E o corpo da que perdeu a corrida:

```json
{
  "type": "https://tools.ietf.org/html/rfc9110#section-15.5.10",
  "title": "Requisicao rejeitada",
  "status": 409,
  "detail": "O saldo de RACE-1789411171 foi alterado por outra operação. Tente novamente.",
  "correlationId": "00-f1706e0054929f0d2e73d2e00e8545fd-20391c80a1a167fd-00"
}
```

O saldo termina em **0**, nunca negativo, e a mensagem nomeia o produto em conflito. Como é
uma corrida, pode ser necessário repetir algumas vezes em máquinas mais lentas.

---

## 3. Ordem de falha

### O problema

Imprimir toca **dois bancos de dados através de HTTP**. Não existe transação distribuída:
o `SaveChanges` do Faturamento e o do Estoque são dois commits independentes. Alguma hora um
vai falhar com o outro já concluído.

Como atomicidade é impossível, a decisão de engenharia deixa de ser *"como evito
inconsistência?"* e passa a ser **"qual estado inconsistente eu prefiro ter?"**.

### A ordem escolhida

```csharp
// backend/Faturamento.Api/Controllers/NotasController.cs
:127   if (nota.Status != StatusNotaFiscal.Aberta) throw new NotaNaoAbertaException(nota);
:142   await estoque.RegistrarBaixaAsync(baixa, cancellationToken);   // externo, arriscado
:144   nota.Status = StatusNotaFiscal.Fechada;
:147   await db.SaveChangesAsync(cancellationToken);                  // local, confiável
```

**Trabalho externo primeiro; commit local por último.** A chamada HTTP é lenta e sujeita a
rede; o `SaveChanges` local é rápido e quase sempre funciona. Se o externo falhar, nada foi
alterado deste lado — repare que a nota só é mutada na linha 144, depois da resposta.

### A assimetria

| Ordem | Estado após a falha | Nome contábil | Custo |
| --- | --- | --- | --- |
| Debitar → fechar | estoque baixado, nota Aberta | estoque **subestimado** | erro de conciliação |
| Fechar → debitar | nota Fechada, estoque intacto | estoque **superestimado** | vende o que não existe |

Subestimar custa uma planilha. Superestimar custa um estorno e um cliente irritado.

### Por que o estado escolhido se recupera sozinho

Com a ordem correta, a falha deixa a nota **Aberta**. No próximo clique:

1. a guarda da linha 127 **deixa passar** — a nota ainda está Aberta;
2. o Estoque encontra `nota-{id}` já registrada e replica a baixa sem debitar
   ([idempotência](#1-idempotência));
3. a nota fecha.

Inverta a ordem e a linha 127 vira armadilha: a nota estaria **Fechada**, o segundo clique
levaria **409**, e o sistema se recusaria a tentar de novo. Só um humano com acesso ao banco
resolveria.

**Debitar primeiro deixa um estado que o sistema conserta sozinho; fechar primeiro deixa um
estado que só um humano conserta.**

### Recusas de regra atravessam intactas

`Faturamento.Api/Infrastructure/EstoqueClient.cs:43` — quando o Estoque responde **422**
(saldo insuficiente, produto inexistente) ou **409** (conflito), o status e o `detail`
são encaminhados sem reescrita, e a nota **continua Aberta**. O usuário lê qual produto
faltou, com o número exato.

### Limite conhecido

Se o Estoque estiver **fora do ar**, hoje a falha vira **500**. O tratamento adequado — 503
com mensagem amigável, mais política de resiliência — está pendente. Ver `NOTES.md`.
