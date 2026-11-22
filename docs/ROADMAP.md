# Roadmap

Documento vivo. Rastreia o que muda no `claude -p` e no stream-json do
[Claude Code](https://github.com/anthropics/claude-code) e pede algo deste repositório: um evento
novo para os parsers, um campo que mudou de forma, um comando novo, uma gravação que precisa ser
refeita. As fases do projeto estão na spec, em [specs/](specs/); aqui fica o que vem de fora.

> **Status legenda:** 🔍 a investigar · 📐 a planejar · 🚧 em andamento · ✅ entregue · ⏸️ adiado (aguardando gatilho) · ❄️ descartado

## Vigia

Duas coisas avisam que o protocolo mudou:

1. **As fixtures.** A partir da fase 2, as gravações são refeitas toda semana com o `claude -p` de
   verdade. Uma gravação que muda abre um pull request, e uma lib que deixa de ler a gravação nova
   quebra o CI.
2. **A varredura.** Um script lê as issues e o CHANGELOG do `anthropics/claude-code` atrás do que
   toca o `-p` e o stream-json. Ele chega ainda na fase 0, e o modo de usar fica nesta seção.

Nenhum dos dois substitui ler o CHANGELOG inteiro desde a última versão vista: o que mais importa
costuma estar numa linha genérica.

## Itens

Nenhum ainda.

## Varreduras

Nenhuma ainda.
