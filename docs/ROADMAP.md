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
2. **A varredura.** O `docs/sweep_issues.py` lê as issues e o CHANGELOG do `anthropics/claude-code`
   atrás do que toca o `-p` e o stream-json. Precisa do `gh` autenticado.

   ```bash
   python docs/sweep_issues.py 2026-11-01         # issues criadas depois da data
   python docs/sweep_issues.py --changelog 2.1.310   # linhas do CHANGELOG depois da versão
   python -I docs/test_sweep_issues.py -v         # os testes das partes puras
   ```

   A varredura de issues grava `sweep_results.json` (fora do git) e imprime as candidatas por
   reações. O que já foi tratado entra na lista `KNOWN` do script, para não voltar.

Nenhum dos dois substitui ler o CHANGELOG inteiro desde a última versão vista: o que mais importa
costuma estar numa linha genérica.

## Itens

Nenhum ainda. As candidatas das varreduras abaixo viram itens depois da triagem.

## Varreduras

### 2026-11-22

A primeira, para provar o script. Nada foi triado: tudo abaixo está em 🔍.

**Issues** (`python docs/sweep_issues.py 2026-11-01`): 150 candidatas nas doze consultas, 63
tocando o protocolo. A consulta pelo rótulo `area:sdk` devolveu zero; o rótulo do repositório é
`area:agent-sdk`, e a consulta foi corrigida depois desta rodada. As que falam direto do que as
libs leem ou escrevem:

- 🔍 [#106811](https://github.com/anthropics/claude-code/issues/106811): `/goal` nunca emite
  `active_goal` no stdout em stream-json (`-p` e Agent SDK).
- 🔍 [#108132](https://github.com/anthropics/claude-code/issues/108132): no modo print, o JSON final
  de sucesso pode esconder uma recusa estruturada anterior.
- 🔍 [#110943](https://github.com/anthropics/claude-code/issues/110943): o modo headless (`-p`) não
  manda a instrução de avisar o usuário sobre prompt injection.
- 🔍 [#107349](https://github.com/anthropics/claude-code/issues/107349): a ferramenta Edit é sempre
  negada com `--permission-mode dontAsk`.
- 🔍 [#109393](https://github.com/anthropics/claude-code/issues/109393): `--json-schema` é ignorado
  em silêncio com `--agent` quando o agente restringe `tools:`.
- 🔍 [#110211](https://github.com/anthropics/claude-code/issues/110211): o timeout de um callback
  `PostToolBatch` do SDK derruba a sessão.

**CHANGELOG** (`python docs/sweep_issues.py --changelog 2.1.310`): 93 linhas tocando o protocolo
nas versões depois da 2.1.310; a mais nova é a 2.1.330. A 2.1.310 é a versão que o README do
Claude Visual Chat cita como referência, e é dele que o código da fase 1 vem. As que mais pedem
leitura antes da fase 2:

- 🔍 2.1.321: o `AskUserQuestion`, em `claude -p` e nas sessões do SDK, passa a negar a resposta de
  permissão de um host que define o campo `afkTimeoutMs` no input.
- 🔍 2.1.323: um heartbeat de progresso a cada 30 segundos enquanto os hooks demoram, em `claude -p`
  e nas sessões do SDK.
- 🔍 2.1.323: `--permission-mode auto` deixa de começar num modo mais permissivo quando o modo auto
  não está disponível.
- 🔍 2.1.325: um valor inválido num `applyFlagSettings()` do Agent SDK passa a ser recusado, em vez
  de descartar em silêncio os ajustes das chamadas anteriores.
- 🔍 2.1.327: sessões headless perdiam mensagens guardadas na retomada quando uma mensagem era
  enviada de novo com o mesmo UUID depois de o processo ser morto.
- 🔍 2.1.328: `CLAUDE_CODE_DISABLE_DEFERRED_TOOL_RESUME`, para um host impedir que
  `claude -p --resume` rode uma chamada de ferramenta que um hook adiou.

Esta lista é uma amostra das 93 linhas, escolhida à mão; a lista inteira sai do comando acima.
