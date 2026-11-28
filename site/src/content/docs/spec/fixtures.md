---
title: Fixtures
description: As gravações de sessões de verdade do claude -p, o resultado esperado de cada uma e como uma lib prova que fala o protocolo.
---

As fixtures são gravações de sessões de verdade do `claude -p`, guardadas em
[`fixtures/`](https://github.com/carlosdealmeida/claude-cli-protocol/tree/master/fixtures). **A
conformidade é delas:** uma lib, em qualquer linguagem, fala o protocolo quando lê cada gravação e
produz o resultado esperado ao lado dela. A lib .NET passa nas 50; a de Delphi vai passar nas mesmas.

## Os arquivos

Quatro arquivos por cenário:

| Arquivo | O que é |
|---|---|
| `<cenário>.stream.ndjson` | O stdout do `claude -p`, uma linha JSON por linha |
| `<cenário>.transcript.jsonl` | O arquivo da mesma sessão, como o Claude Code o grava em disco |
| `<cenário>.stream.expected.json` | Os eventos que saem do `.stream.ndjson` |
| `<cenário>.transcript.expected.json` | Os eventos que saem do `.transcript.jsonl` |

As gravações guardam só o que o `claude` escreveu. O que o gravador mandou no stdin está no roteiro
de cada cenário, em `tools/cenarios.json`.

## Os cenários

| Cenário | O que o roteiro faz | O que a gravação mostra |
|---|---|---|
| `edit` | Pede um `Edit` e um `Write`; permite os dois | Dois pedidos de permissão, os resultados das ferramentas |
| `bash` | Pede dois comandos de shell; permite | Pedidos com `addRules` nas sugestões, `decision_reason_type: subcommandResults` |
| `subagent` | Pede um sub-agent que lê três arquivos | `system/task_*`, um pedido com `agent_id`, a tarefa que termina depois do `result` |
| `error` | Pede para ler um arquivo que não existe | Um `tool_result` de erro |
| `denied` | Pede um `Write`; nega com uma mensagem | A mensagem como `tool_result` de erro |
| `denied-long` | O mesmo, com uma mensagem de quatro linhas | A mensagem longa, com as quebras |
| `interrupted` | Pede um comando longo e manda `interrupt` sem responder ao pedido | `control_cancel_request`, `still_queued`, `result` de `error_during_execution` |
| `mode-and-model` | Troca o modo e o modelo, depois pede um `Write`; permite sempre | As respostas de `set_permission_mode` e `set_model`, o "permitir sempre" |
| `image` | Manda uma imagem PNG com uma pergunta | Um `tool_result` com imagem (gravação do 2.1.286) |
| `commands` | Pede o uso do contexto e roda `/hello Ana` e `/compact` | A resposta de `get_context_usage`, `<local-command-stdout>`, a compactação |
| `effort` | Troca o effort e o modelo, e consulta os ajustes | `apply_flag_settings` sem corpo, `get_settings` com `applied` |
| `plan` | Pede um plano em modo plan; troca para `acceptEdits` e permite o `ExitPlanMode` | A saída do plan |
| `plan-auto` | O mesmo, trocando para `auto` | A saída do plan para o modo auto |
| `plan-feedback` | Pede um plano; nega o `ExitPlanMode` com uma mensagem | O plano que volta ao modelo, um `AskUserQuestion` |
| `queued` | Manda uma segunda mensagem enquanto a primeira roda um comando | Duas mensagens, um turno: um `system/init` e um `result` |
| `queued-compact` | Manda `/compact` sem esperar e uma mensagem depois | `compact_boundary`, o `attachment` de `queued_command` no arquivo |
| `hook-blocked` | Manda um prompt que um hook `UserPromptSubmit` bloqueia | `system/informational` de nível `warning` |
| `hook-ask` | Um hook `PreToolUse` pede confirmação antes de um `Write` | `decision_reason_type: hook`, com o texto do hook |
| `error-long` | Roda um comando que imprime 40 linhas no stderr e sai com 3 | Um resultado de erro longo, `decision_reason` de conferência |
| `error-wrapped` | Pede um `Edit` de um texto que não existe | O erro da ferramenta |
| `seguranca` | Pede um `Write` em `.claude/settings.local.json`; nega | `decision_reason_type: safetyCheck` |
| `bash-ansi` | Roda um `printf` com cores de terminal, um sinal sonoro e um caractere de largura zero | Um resultado de ferramenta com códigos de terminal e caracteres invisíveis |
| `advisor` | Pede uma chamada ao advisor, com `--advisor opus` | Os blocos `server_tool_use` e `advisor_tool_result`, com o veredito cifrado |
| `workflow` | Pede um workflow de uma fase, com um agent | `system/background_tasks_changed` com `local_workflow`, as linhas `system/task_*` do workflow |
| `stop-task` | Roda `sleep 60` em segundo plano e manda `stop_task` dois segundos depois | `task_notification` com `status: stopped`, a lista de tarefas vazia, a resposta do `stop_task` |

São 25 cenários gravados. O roteiro tem um 26º, `subagent-permission`, que nunca foi gravado.
Vinte gravações são do Claude Code 2.1.331; a do `image` é do 2.1.286, porque na regravação de
2026-11-25 o modelo respondeu sem abrir a imagem, e a gravação anterior é a única com um resultado
de ferramenta que traz uma imagem. As de `bash-ansi`, `advisor`, `workflow` e `stop-task` são do
2.1.330: foram gravadas no Claude Visual Chat e vieram de lá.

## O resultado esperado

Um JSON neutro, sem nada que seja de uma linguagem só. A descrição completa está no
[README das fixtures](https://github.com/carlosdealmeida/claude-cli-protocol/blob/master/fixtures/README.md#o-formato-do-resultado-esperado-versão-1);
o essencial:

<!-- fixture: edit.stream.expected.json:8 -->
```json
{"line": 2, "event": "session_started", "fields": {"session_id": "8f8ab1b3-6d82-4572-9c2c-6f5f166d7bd2", "model": "claude-haiku-5-5", "working_directory": "C:\\demo\\app", "permission_mode": "default", "claude_version": "2.1.331", "terminal_commands": [], "fast_mode_state": null, "ultrathink_available": null}}
```

- **`format`: 1**, e a lista `events`, um evento por linha do arquivo.
- **`line`** é a linha da gravação, contando do 1 e sem contar as linhas em branco. Uma linha pode
  dar nenhum, um ou mais eventos, e a ordem dos eventos de uma linha importa.
- **`event`** e as chaves de **`fields`** são os nomes de [Eventos](../eventos/). O conjunto de
  chaves de cada evento é fixo; um campo sem valor vai `null`.
- **Enum** pelo nome em `snake_case` (`input_json`). **Duração** em milissegundos inteiros.
  **Data** em ISO 8601, UTC, com exatamente três casas de milissegundo e `Z`, cortada e não
  arredondada.
- **Imagem:** o `data` vira `{"length": <caracteres do base64>, "sha256": "<hex minúsculo dos bytes
  UTF-8 do texto base64>"}`.
- **A comparação é estrutural.** A ordem das chaves não importa; a dos itens de uma lista, sim. Dois
  números são iguais quando diferem em no máximo `1e-9`.

## Como uma lib prova a conformidade

1. Para cada `<cenário>.stream.ndjson`: um leitor novo, **com estado entre as linhas** (o id do
   último `message_start` de cada `parent_tool_use_id`), lê o arquivo linha a linha.
2. Para cada `<cenário>.transcript.jsonl`: um leitor **sem estado** lê cada linha sozinha.
3. A lib escreve os eventos no formato acima e compara, estruturalmente, com o `.expected.json`.
4. Uma diferença é uma falha, com o caminho até o valor diferente
   (`events[12].fields.block.input.file_path`, por exemplo).

As linhas são separadas por `\n`; um `\n` no fim é ignorado, e uma linha em branco não conta.

### O que passar prova

Os 50 resultados esperados foram escritos pela lib .NET. Passar neles prova que uma lib lê as
gravações como a lib .NET lê, e que uma mudança de comportamento aparece como diferença. Não prova,
sozinho, que cada campo está certo. Três cenários (`edit`, `denied` e `mode-and-model`) foram
conferidos por fora, com um script que lê a gravação crua. As gravações exercitam 28 dos 37 tipos
de evento dos leitores; os outros nove estão em
[Eventos](../eventos/#os-que-nenhuma-gravação-exercita).

## A higienização

Uma gravação é uma sessão de verdade, e nenhuma entra com dado pessoal de quem gravou. O gravador
(`tools/gravar.py`):

- troca a pasta, a home e o nome do usuário pelos de `demo` (`C:\demo\app`,
  `C:\Users\demo`), também quando um caminho chega partido entre vários deltas de um bloco;
- troca pelo nome da pasta de `demo` (`C--demo-app`) a pasta de projeto que o Claude Code cria sob
  `Temp\claude`, cujo nome, desde o 2.1.330, são 16 dígitos hexadecimais tirados da pasta de quem
  gravou;
- troca por `redacted` todo `encrypted_content`, como o do veredito do advisor;
- deixa na linha `system/init` só os campos conhecidos, e tira as ferramentas `mcp__`;
- deixa nas respostas do `initialize`, do `get_settings` e do `get_context_usage` só os campos
  conhecidos, e tira a conta;
- tira as linhas `rate_limit_event`, que são da conta de quem gravou, e o `credential_expires_at` do
  `result`.

O teste de privacidade (`FixturePrivacyTests`) procura em cada arquivo uma pasta de outro usuário,
uma pasta de projeto com nome em hash, o que pareça uma credencial e um endereço de e-mail, e diz em que linha e em que campo achou, sem
mostrar o valor. Nenhum dos dois entende o texto da conversa: quem grava lê a gravação antes de
mandar.

## A regravação semanal

Toda segunda-feira, o workflow `fixtures.yml` grava de novo, com o `claude` mais novo, os cenários
que já têm gravação, em dois jobs:

1. o que roda o `claude` de verdade, sem permissão de escrita no repositório: grava, escreve os
   resultados esperados de novo e roda os testes de conformidade e de privacidade sobre os arquivos
   novos;
2. o que abre o pull request com as gravações e os esperados juntos, sem rodar o `claude`.

Os ids de sessão e de mensagem mudam a cada gravação, então há um pull request por semana. Como os
esperados são escritos pela própria lib, quem barra uma gravação ruim é a pessoa que lê o pull
request: um evento que some, um campo que muda de forma. **É assim que uma mudança do CLI aparece
aqui antes de aparecer em uso.** A primeira regravação que foi até o fim foi a de 2026-11-25.

## Gravar à mão

```bash
python -I tools/gravar.py edit        # um cenário
python -I tools/gravar.py --todos     # todos
python -I tools/test_gravar.py        # os testes do higienizador (não sobem o claude)
```

O gravador sobe o `claude` do PATH (ou o de `--claude <caminho>`) numa pasta temporária, com a conta
da máquina. Depois de gravar, os esperados são escritos de novo pela lib .NET:

```bash
CCP_UPDATE_EXPECTED=1 dotnet test libs/dotnet -c Release --framework net6.0 --filter "FullyQualifiedName~ConformanceTests"
```
