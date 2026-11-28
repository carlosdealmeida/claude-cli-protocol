---
title: Protocolo
description: Como o claude -p é chamado, a forma de uma linha, os tipos de linha do stdout e o turno.
---

O `claude -p` com `--input-format stream-json` e `--output-format stream-json` é um processo que
lê uma linha JSON por comando no stdin e escreve uma linha JSON por evento no stdout. Esta página
diz o que vem no stdout. O que vai no stdin está em [Comandos](../comandos/) e em
[Controle](../controle/); o processo, em [Transporte](../transporte/).

:::caution[O protocolo não tem contrato]
A Anthropic não documenta este protocolo como interface estável
([anthropics/claude-code#111046](https://github.com/anthropics/claude-code/issues/111046)). O que
está aqui foi conferido nas gravações de `fixtures/`, feitas com o Claude Code 2.1.331 (o cenário
`image`, com o 2.1.286; `advisor`, `bash-ansi`, `stop-task` e `workflow`, com o 2.1.330), e no
código da lib .NET. Uma versão nova pode mudar qualquer linha. A
regravação semanal das fixtures é o que avisa.
:::

## A chamada

```
claude -p --input-format stream-json --output-format stream-json --verbose
       --include-partial-messages --permission-prompt-tool stdio --permission-mode <modo>
```

- `--verbose` é obrigatório: sem ele, o `claude -p` recusa o `--output-format stream-json`.
- `--include-partial-messages` faz ele escrever os pedaços da resposta enquanto ela chega (as
  linhas `stream_event`).
- `--permission-prompt-tool stdio` faz ele pedir permissão pelo próprio stdout, com um
  `control_request` de `can_use_tool`, e esperar a resposta no stdin.

Os opcionais (`--resume`, `--model`, `--effort`) e a conferência de cada valor estão em
[Transporte](../transporte/#os-argumentos).

## A linha

- **Uma linha, um objeto JSON**, terminada em `\n`. O stdout é UTF-8.
- **O campo `type`** diz o que a linha é. Em `system`, o `subtype` diz qual aviso; em
  `stream_event`, o `event.type`; em `control_request`, o `request.subtype`.
- **Quase toda linha traz `session_id` e `uuid`.** O `session_id` é o da sessão; o `uuid`, o da
  linha.
- **Um leitor não levanta exceção.** Linha em branco, linha que não começa com `{` (depois de tirar
  espaços e códigos de terminal do começo), JSON inválido, `type` desconhecido ou campo que falta:
  a linha não gera evento, e a leitura segue. É assim que os dois leitores da lib .NET fazem, e é o
  que deixa uma lib de pé quando o `claude` ganha uma linha nova.
- **Campo desconhecido é ignorado.** As linhas trazem muito mais campos do que um leitor usa (o
  `result` tem mais de vinte). Um leitor lê os que conhece.

## Os tipos de linha

Contagem nas 25 gravações do stdout (`fixtures/*.stream.ndjson`):

| `type` | Subtipo | Nas gravações | O que é |
|---|---|---:|---|
| `system` | `init` | 33 | O começo de um turno: sessão, modelo, pasta, modo, versão |
| `system` | `status` | 74 | O que o `claude` está fazendo (`requesting`, `compacting`, `null`) e o modo |
| `system` | `thinking_tokens` | 30 | Quantos tokens o `claude` pensou até agora no bloco que está chegando |
| `system` | `task_started`, `task_progress`, `task_updated`, `task_notification` | 15 | Uma tarefa em segundo plano: um sub-agent, um comando ou um workflow |
| `system` | `compact_boundary` | 2 | A conversa foi compactada |
| `system` | `informational` | 1 | Um aviso do `claude` sobre o próprio trabalho (um hook que bloqueou o prompt, por exemplo) |
| `system` | `background_tasks_changed` | 6 | A lista inteira das tarefas em segundo plano, a cada mudança |
| `stream_event` | `message_start` | 61 | Uma resposta começou a chegar |
| `stream_event` | `content_block_start`, `content_block_delta`, `content_block_stop` | 1.100 | Os blocos da resposta, pedaço por pedaço |
| `stream_event` | `message_delta`, `message_stop` | 122 | O fim da resposta. A lib não lê |
| `assistant` | | 97 | Um bloco inteiro da resposta |
| `user` | | 48 | O que volta ao modelo: o resultado de uma ferramenta, um texto do `claude` |
| `control_request` | `can_use_tool` | 20 | Um pedido de permissão |
| `control_cancel_request` | | 1 | O `claude` desistiu de um pedido de permissão |
| `control_response` | `success` | 41 | A resposta a um pedido de controle do host |
| `result` | `success`, `error_during_execution` | 33 | O fim de um turno |

Há linhas que a lib lê e que nenhuma gravação tem. Em `system`: `api_retry`, `hook_started`,
`hook_response`, `commands_changed`, `bridge_state`, `local_command`, `per_turn_effort_changed`,
`model_refusal_fallback` e `peer_message_hold`. Fora de `system`: `rate_limit_event` (o gravador a
tira, porque é da conta de quem gravou) e `command_lifecycle`. Os eventos que saem delas estão em
[Eventos](../eventos/), marcados como não gravados.

## O começo do turno: `system/init`

Uma linha `system/init` abre cada turno, não cada processo. Nas 25 gravações, os `system/init` e
os `result` se alternam, um para um.

<!-- fixture: edit.stream.ndjson:2 -->
```json
{"type": "system", "subtype": "init", "session_id": "8f8ab1b3-6d82-4572-9c2c-6f5f166d7bd2", "model": "claude-haiku-5-5", "cwd": "C:\\demo\\app", "permissionMode": "default", "claude_code_version": "2.1.331", "tools": ["Task", "AskUserQuestion", "Bash", "…"]}
```

O que um leitor tira dela: `session_id`, `model`, `cwd`, `permissionMode` e `claude_code_version`;
quando vêm, `terminal_slash_commands`, `fast_mode_state` e `ultrathink_available`. As gravações
guardam só os campos conhecidos da linha: o gravador tira o resto (ver [Fixtures](../fixtures/#a-higienização)).

:::note
O modo que vale é o desta linha, não o que foi pedido na linha de comando. Desde a 2.1.323, um
`--permission-mode auto` começa em `default` quando o auto não está disponível
([#111343](https://github.com/anthropics/claude-code/issues/111343)).
:::

## A resposta enquanto chega: `stream_event`

Com `--include-partial-messages`, cada resposta do modelo vem primeiro em pedaços, no formato de
streaming da API da Anthropic, dentro de `event`:

<!-- fixture: edit.stream.ndjson:5 -->
```json
{"type": "stream_event", "event": {"type": "content_block_start", "index": 0, "content_block": {"type": "tool_use", "id": "toolu_015zZi1E7vQS17Zr1yMU1XbP", "name": "Read", "input": {}}}, "parent_tool_use_id": null}
```

<!-- fixture: edit.stream.ndjson:7 -->
```json
{"type": "stream_event", "event": {"type": "content_block_delta", "index": 0, "delta": {"type": "input_json_delta", "partial_json": "{\"file_"}}, "parent_tool_use_id": null}
```

- **`message_start`** traz `event.message.id`, o id da resposta. Os deltas que vêm depois não
  trazem esse id: **um leitor guarda o id do último `message_start` de cada `parent_tool_use_id`**
  e o põe nos eventos dos blocos. Por isso o leitor do stdout tem estado, e o do arquivo da sessão
  não.
- **`content_block_start`** abre o bloco `index`. Um `tool_use` começa com `input` vazio (`{}`).
- **`content_block_delta`** estende o bloco. O `delta.type` diz qual parte: `text_delta` (campo
  `text`), `thinking_delta` (campo `thinking`, e `estimated_tokens` quando vem) ou
  `input_json_delta` (campo `partial_json`, um pedaço do JSON do `input`, que só fecha no fim).
- **`content_block_stop`** fecha o bloco.
- **`message_delta`** e **`message_stop`** fecham a resposta. A lib não os lê: a linha
  `assistant` traz o bloco inteiro.
- **`parent_tool_use_id`** é a chamada de ferramenta do sub-agent que escreve a resposta, ou `null`
  para a conversa principal.

## O bloco inteiro: `assistant`

Depois dos pedaços, o `claude` escreve a resposta inteira em linhas `assistant`, no formato de
mensagem da API: `message.id`, `message.content` (a lista de blocos) e `message.usage`. Nas
gravações cada linha `assistant` tem um bloco só, e uma resposta com três blocos vira três linhas
com o mesmo `message.id`.

<!-- fixture: edit.stream.ndjson:15 -->
```json
{"type": "assistant", "message": {"id": "msg_011ChNjwF76zsXQUUbNF7yfr", "role": "assistant", "content": [{"type": "tool_use", "id": "toolu_015zZi1E7vQS17Zr1yMU1XbP", "name": "Read", "input": {"file_path": "C:\\demo\\app\\notes.md"}}]}, "parent_tool_use_id": null}
```

Os blocos que um leitor conhece: `text`, `thinking`, `tool_use`, `tool_result`, `image` e os dois
do advisor. Um `message.model` igual a `<synthetic>` marca uma linha escrita pelo próprio `claude`,
não pelo modelo.

O advisor é uma ferramenta do servidor. A chamada é um bloco `server_tool_use` de nome `advisor`, e
o veredito, um bloco `advisor_tool_result`, vem numa linha `assistant`, não numa `user`:

<!-- fixture: advisor.stream.ndjson:10 -->
```json
{"type": "assistant", "message": {"id": "msg_011ChJm4RCsC4MLQpjNGC4wg", "role": "assistant", "content": [{"type": "advisor_tool_result", "tool_use_id": "srvtoolu_01UsaBcWLXWZgtjtrmoWaNKX", "content": {"type": "advisor_redacted_result", "encrypted_content": "redacted"}}]}}
```

- O `content.type` do veredito é `advisor_result` (com o `text`, e `stop_reason: "refusal"` quando
  o advisor se recusa), `advisor_redacted_result` (cifrado, sem texto para ler; o gravador troca o
  `encrypted_content` por `redacted`) ou `advisor_tool_result_error` (com o `error_code`). Só o
  segundo está gravado.
- Um `server_tool_use` de outro nome não dá bloco, e um veredito de `content.type` desconhecido
  também não.

## O que volta ao modelo: `user`

O resultado de uma ferramenta volta numa linha `user`, com um bloco `tool_result` em
`message.content` e o resultado estruturado da ferramenta em `tool_use_result`:

<!-- fixture: edit.stream.ndjson:19 -->
```json
{"type": "user", "message": {"role": "user", "content": [{"tool_use_id": "toolu_015zZi1E7vQS17Zr1yMU1XbP", "type": "tool_result", "content": "1\tline one\n…"}]}, "parent_tool_use_id": null, "tool_use_result": {"type": "text", "file": {"filePath": "C:\\demo\\app\\notes.md"}}, "tool_result_meta": [{"id": "toolu_015zZi1E7vQS17Zr1yMU1XbP", "permission_decision": {"decision": "accept", "source": "config", "reason_type": "mode"}}]}
```

- O `content` de um `tool_result` é um texto ou uma lista de blocos (texto e imagem). O leitor junta
  os textos com `\n` e guarda as imagens à parte.
- `is_error: true` marca um resultado de erro. `tool_result_meta[].non_execution_kind` diz por que
  a ferramenta não rodou, quando não rodou: `permission-rule` (negada) ou `user-rejected`
  (interrompida).
- O `claude` também escreve em linhas `user` textos que são dele: a marcação de um comando de barra
  (`<local-command-stdout>`), o resumo depois de uma compactação, `[Request interrupted by user for
  tool use]`. A mensagem que o host manda não volta no stdout como linha `user` (só voltaria com
  `--replay-user-messages`, que a lib não passa).

## O fim do turno: `result`

<!-- fixture: edit.stream.ndjson:75 -->
```json
{"type": "result", "subtype": "success", "is_error": false, "duration_ms": 3039, "duration_api_ms": 2777, "num_turns": 4, "total_cost_usd": 0.003943445, "usage": {"input_tokens": 6, "cache_creation_input_tokens": 25845, "cache_read_input_tokens": 50572, "output_tokens": 413}, "fast_mode_state": "off", "result": "Done. Line 3 of `notes.md` now reads `line 3 (edited)`, and `todo.md` was created with two bullet items: \"First item\" and \"Second item\"."}
```

- `subtype` é `success` ou um erro; nas gravações, o único erro é `error_during_execution`
  (o turno interrompido), com `is_error: true` e a lista `errors`.
- Os tokens de entrada que um leitor soma: `input_tokens` + `cache_creation_input_tokens` +
  `cache_read_input_tokens`. Os de saída: `output_tokens`.
- O texto de um turno que falhou: `result`, se tem; senão, o primeiro texto de `errors`.
- `duration_api_ms` é contado desde o começo do processo, não do turno.

:::caution
O `result` de sucesso pode esconder uma recusa anterior do turno
([#108132](https://github.com/anthropics/claude-code/issues/108132)). Quem precisa saber o que foi
negado lê os pedidos de permissão e os `tool_result`, não só o `result`.
:::

## O turno

Um turno vai de uma mensagem do usuário ao `result` dela:

1. o host escreve a mensagem no stdin;
2. o `claude` escreve `system/init` e `system/status` (`requesting`);
3. para cada resposta do modelo: `stream_event` (`message_start`, os blocos, `message_stop`) e as
   linhas `assistant` dos blocos;
4. para cada ferramenta: o `control_request` de `can_use_tool`, quando precisa de permissão, e a
   linha `user` com o resultado;
5. o `result`.

**Uma mensagem mandada no meio do turno não abre outro.** No cenário `queued`, foram duas
mensagens, a segunda enquanto a primeira rodava um comando; o `claude` levou a segunda para dentro
do mesmo turno, e o stream tem um `system/init` e um `result` só. Contar mensagens enviadas contra
`result` recebidos não fecha. Ver "ocupada" em [Transporte](../transporte/#ocupada).

As tarefas em segundo plano (`system/task_*`) podem terminar depois do `result` do turno que as
abriu: no cenário `subagent`, o `task_notification` chega na linha 76, depois do `result` da
linha 72.

## O arquivo da sessão

O Claude Code também grava cada sessão em disco, num `.jsonl` (o "arquivo da sessão"). Ele não é
o stdout: é outro formato, com outras linhas, e serve para ler uma conversa que já aconteceu.

- **Os tipos:** `user`, `assistant`, `system` e `attachment`. Nas 25 gravações
  (`fixtures/*.transcript.jsonl`) aparecem `user` (84), `assistant` (95), `system` com
  `compact_boundary` (2) e `informational` (1), e um `attachment` de `queued_command` (1).
- **Os nomes mudam.** O resultado estruturado da ferramenta é `toolUseResult`, não
  `tool_use_result`; o id da sessão é `sessionId`; os metadados da compactação podem vir como
  `compactMetadata`, com `preTokens` e `postTokens`.
- **Não tem os pedaços.** Nada de `stream_event`, de `control_request` nem de `result`: só os blocos
  inteiros.
- **A mensagem do usuário está lá**, como linha `user` com o texto, o que não acontece no stdout.
  Uma mensagem entregue no meio de um turno fica como `attachment` (`queued_command`).
- **O leitor não tem estado:** cada linha se lê sozinha.

<!-- fixture: edit.transcript.jsonl:1 -->
```json
{"type": "user", "uuid": "8b8a5b2f-b887-4d8f-baa5-fd637c77371a", "parentUuid": null, "isSidechain": false, "timestamp": "2026-11-25T00:59:51.422Z", "sessionId": "8f8ab1b3-6d82-4572-9c2c-6f5f166d7bd2", "message": {"role": "user", "content": "Use the Edit tool to change line 3 of notes.md to 'line 3 (edited)'. Then use the Write tool to create todo.md with two bullet items. Be brief."}}
```

Os eventos que saem de cada formato estão em [Eventos](../eventos/), com a coluna de origem.
