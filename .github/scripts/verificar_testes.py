#!/usr/bin/env python3
"""
Le os arquivos .trx produzidos por `dotnet test` e prova o que rodou.

Existe por um motivo concreto: um passo de teste verde nao diz QUAIS testes rodaram. Se a
pista de PostgreSQL parasse de executar — Docker indisponivel, colecao renomeada, filtro mal
escrito — o CI continuaria verde e as garantias de idempotencia, concorrencia e atomicidade
voltariam a nao ser verificadas, sem ninguem perceber.

Falha a build quando nenhum teste da pista PostgreSQL executa.
"""
import os
import sys
import xml.etree.ElementTree as ET
from pathlib import Path

NS = {"t": "http://microsoft.com/schemas/VisualStudio/TeamTest/2010"}
MARCADOR_PISTA_REAL = "Postgres"


def coletar(raiz: Path):
    testes = []
    arquivos = sorted(raiz.rglob("*.trx"))
    for arq in arquivos:
        raiz_xml = ET.parse(arq).getroot()
        for r in raiz_xml.findall(".//t:UnitTestResult", NS):
            testes.append((r.get("testName", ""), r.get("outcome", "")))
    return arquivos, testes


def main() -> int:
    raiz = Path(sys.argv[1] if len(sys.argv) > 1 else "TestResults")
    if not raiz.is_dir():
        print(f"ERRO: diretorio de resultados nao encontrado: {raiz}")
        return 1

    arquivos, testes = coletar(raiz)
    if not testes:
        print(f"ERRO: nenhum resultado de teste encontrado em {raiz}")
        return 1

    total = len(testes)
    passou = sum(1 for _, o in testes if o == "Passed")
    falhou = sum(1 for _, o in testes if o == "Failed")
    pulou = total - passou - falhou

    pista_real = [(n, o) for n, o in testes if MARCADOR_PISTA_REAL in n]
    real_ok = sum(1 for _, o in pista_real if o == "Passed")

    linhas = [
        "## Testes",
        "",
        f"| | |",
        f"| --- | --- |",
        f"| Arquivos .trx lidos | {len(arquivos)} |",
        f"| Total | **{total}** |",
        f"| Passaram | {passou} |",
        f"| Falharam | {falhou} |",
        f"| Pulados | {pulou} |",
        "",
        f"### Pista PostgreSQL real — {len(pista_real)} teste(s)",
        "",
    ]
    if pista_real:
        linhas += ["| Teste | Resultado |", "| --- | --- |"]
        linhas += [f"| `{n.rsplit('.', 1)[-1]}` | {o} |" for n, o in sorted(pista_real)]
    else:
        linhas.append("**NENHUM** — estes testes nao executaram.")
    linhas.append("")

    relatorio = "\n".join(linhas)
    print(relatorio)

    if resumo := os.environ.get("GITHUB_STEP_SUMMARY"):
        with open(resumo, "a", encoding="utf-8") as f:
            f.write(relatorio + "\n")

    if not pista_real:
        print(
            "ERRO: nenhum teste da pista PostgreSQL executou.\n"
            "      A suite fica verde sem verificar idempotencia, concorrencia nem\n"
            "      atomicidade. Docker indisponivel no runner, ou a colecao foi renomeada."
        )
        return 1

    if real_ok != len(pista_real):
        print(f"ERRO: {len(pista_real) - real_ok} teste(s) da pista PostgreSQL nao passaram.")
        return 1

    print(f"OK: {len(pista_real)} teste(s) da pista PostgreSQL executaram e passaram.")
    return 0


if __name__ == "__main__":
    sys.exit(main())
