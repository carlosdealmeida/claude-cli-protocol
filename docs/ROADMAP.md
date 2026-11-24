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

Triados na varredura de 2026-11-24. O número de versão é a do Claude Code em que a mudança saiu.

### Para a fase 2 (o transporte que sobe o `claude`)

1. ✅ **O código de saída é o único sinal de uma partida que falhou.** A lib lê e descarta o stderr.
   Desde a 2.1.314 o `claude -p` imprime o erro e sai com 1 quando a partida falha (antes saía com 0
   e sem saída); a 2.1.317, a 2.1.319, a 2.1.321 e a 2.1.330 fizeram o mesmo para disco cheio e para
   o modo auto recusado. Feito na fase 2: o `ProcessTransport` entrega o código em `ExitCode`.
2. ✅ **Não fechar o stdin com mensagem pendente.** [#111427](https://github.com/anthropics/claude-code/issues/111427): com `--input-format stream-json`, o
   fim do stdin depois de um turno encerra o processo na hora e perde o que estava pendente. Fechar
   o stdin é como se encerra, e só se encerra de propósito. Feito na fase 2: o `ProcessTransport`
   nunca fecha o stdin sozinho; ele só encerra o `claude` quando é descartado.
3. 🔍 **Resposta de permissão com `input` grande.** [#109249](https://github.com/anthropics/claude-code/issues/109249): com `--permission-prompt-tool`, um
   resultado grande de "allow" é gravado em arquivo pelo tratamento de saída grande do MCP. A
   resposta da lib devolve o `input` inteiro, então um `Write` de arquivo grande passa por aí.
   Conferir com um `claude` de verdade.
4. 🔍 **Batida de progresso a cada 30 segundos enquanto os hooks demoram** (2.1.323), em `claude -p`
   e nas sessões do SDK. Os leitores ignoram linha que não conhecem, então nada quebra; falta ver
   a forma da linha numa gravação.
5. 🔍 **Mensagem enviada no meio do turno.** [#110808](https://github.com/anthropics/claude-code/issues/110808) (a mensagem é fundida na saída de um Skill
   sem separador), a 2.1.318 (o Stop passa a cancelar a mensagem enviada logo depois de uma troca de
   modelo) e a 2.1.327 (reenviar uma mensagem com o mesmo UUID depois de o processo morrer perdia
   mensagens guardadas). Sustenta a decisão 9 da spec, e pede uma regra: nunca reenviar uma mensagem
   com o `uuid` de outra.
6. 🔍 **Prompt com marcação não era confirmado ao host** (2.1.314): prompts com tags como
   `<bash-stdout>` não eram confirmados a um host stream-json local. Conferir o que a lib vê quando
   manda uma mensagem assim com `uuid`.
7. ✅ **Variáveis de ambiente novas que um host pode querer passar:**
   `CLAUDE_CODE_DISABLE_DEFERRED_TOOL_RESUME` (2.1.328), `CLAUDE_CODE_FORK_SUBAGENT` (2.1.328) e
   `CLAUDE_CODE_DISABLE_NESTED_SESSION_ID` (2.1.317). O transporte passa adiante o ambiente que
   recebe; não conhece nenhuma delas pelo nome. Feito na fase 2: `ProcessTransportOptions.Environment`.
8. 🔍 **Demora na partida.** A 2.1.325 passou a esperar os dados da conta antes do primeiro pedido, a
   2.1.327 espera até 10 segundos pelos ajustes do modelo, e [#109534](https://github.com/anthropics/claude-code/issues/109534) relata 5 a 7 segundos até o
   primeiro byte desde a 2.1.323. Pesa nos prazos do gravador e do exemplo.
9. 🔍 **No Windows, o `claude` sem argumentos saía com "Input must be provided … --print"** no Git
   Bash ([#110401](https://github.com/anthropics/claude-code/issues/110401), 2.1.325): ele decide o modo pelo que o stdin é. O transporte sempre passa `-p`
   e um pipe, então não cai nisso; fica anotado para o exemplo.

### Já cobertos, sem ação

10. ✅ **Escapes `\uXXXX` nas strings do stream-json** (2.1.311): DEL, controles C1 e caracteres de
    formato do Unicode passam a sair escapados; o valor lido é o mesmo. Os leitores leem JSON.
11. ✅ **O `AskUserQuestion` nega a resposta de permissão de um host que mexe no `input`** (2.1.321,
    campo `afkTimeoutMs`). A regra da lib já é devolver o `input` como veio.
12. ✅ **Um valor inválido no `apply_flag_settings` passa a ser recusado** (2.1.325). A resposta de
    erro chega como qualquer outra, no `ControlReplied`.
13. ✅ **`--permission-mode auto` começa em Manual quando o auto não está disponível** (2.1.323,
    [#111343](https://github.com/anthropics/claude-code/issues/111343)). A sessão lê o modo da linha `system/init`, não do que foi pedido.
14. ✅ **`credential_expires_at` na linha `result`** (2.1.318). O gravador tira o campo, e o teste de
    privacidade o proíbe nas gravações.

### Para acompanhar

15. 🔍 [#111046](https://github.com/anthropics/claude-code/issues/111046): pedido para a Anthropic tornar o protocolo de controle do stream-json e o
    `--permission-prompt-tool stdio` uma interface estável e documentada. É o motivo deste
    repositório existir, e também o risco dele: hoje o protocolo não tem contrato.
16. 🔍 [#106811](https://github.com/anthropics/claude-code/issues/106811): `/goal` nunca emite `active_goal` no stdout em stream-json.
17. 🔍 [#108132](https://github.com/anthropics/claude-code/issues/108132): no modo print, o JSON final de sucesso pode esconder uma recusa anterior.
18. 🔍 [#110490](https://github.com/anthropics/claude-code/issues/110490): um `side_question` mandado cedo num turno devolve a resposta do turno. É um
    pedido de controle que a lib ainda não tem.
19. 🔍 [#110918](https://github.com/anthropics/claude-code/issues/110918): pedido para carregar um histórico pelo stdin em stream-json sem um ciclo de
    `init` e `result` por mensagem.
20. 🔍 [#110943](https://github.com/anthropics/claude-code/issues/110943), [#107349](https://github.com/anthropics/claude-code/issues/107349), [#109393](https://github.com/anthropics/claude-code/issues/109393), [#110211](https://github.com/anthropics/claude-code/issues/110211), [#110540](https://github.com/anthropics/claude-code/issues/110540), [#111159](https://github.com/anthropics/claude-code/issues/111159), [#111811](https://github.com/anthropics/claude-code/issues/111811), [#107839](https://github.com/anthropics/claude-code/issues/107839):
    comportamentos do modo headless que não mudam o que a lib lê ou escreve.

### Descartados

21. ❄️ Cobrança e limites do plano em uso headless ([#107106](https://github.com/anthropics/claude-code/issues/107106), [#107134](https://github.com/anthropics/claude-code/issues/107134), [#108851](https://github.com/anthropics/claude-code/issues/108851)): a lib não
    trata de conta. Code Mode, computer use, interface de mods e ditado no Agent SDK ([#109679](https://github.com/anthropics/claude-code/issues/109679),
    [#106877](https://github.com/anthropics/claude-code/issues/106877), [#106868](https://github.com/anthropics/claude-code/issues/106868), [#108577](https://github.com/anthropics/claude-code/issues/108577), [#110977](https://github.com/anthropics/claude-code/issues/110977)): não são do protocolo do `claude -p` que a lib fala
    hoje.

## Varreduras

### 2026-11-24

Feita com o `claude` 2.1.331 instalado, antes de planejar a fase 2. Desta vez com triagem: o que
saiu dela está em **Itens**, acima.

- **Issues** (`python docs/sweep_issues.py 2026-11-01`): 165 candidatas nas doze consultas, 71
  tocando o protocolo. As 70 primeiras foram lidas pelo título; as que viraram item ou foram
  descartadas entraram na lista `KNOWN` do script.
- **CHANGELOG** (`python docs/sweep_issues.py --changelog 2.1.310`): 95 linhas tocando o protocolo,
  da 2.1.311 à 2.1.331. Todas foram lidas. Nenhuma muda a forma de uma linha que os leitores leem;
  a única que fala do formato é a dos escapes `\uXXXX` (2.1.311).

O que a varredura não cobre: ela lê títulos de issue e linhas do CHANGELOG. A forma de cada linha
só se confirma gravando com o `claude` de verdade. As gravações deste repositório foram feitas com
quatro versões, pelo que as linhas `system/init` registram: 2.1.286 (a maior parte), 2.1.310,
2.1.323 e 2.1.329. Nenhuma é da 2.1.331. Regravar é parte da fase 2.

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
