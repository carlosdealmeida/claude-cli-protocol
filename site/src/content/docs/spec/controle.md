---
title: Controle
description: Os pedidos de controle nos dois sentidos — do host ao claude e do claude ao host —, o pedido de permissão, a resposta e a regra dela.
---

Um pedido de controle é uma linha `control_request` com um `request_id`, e a resposta é uma linha
`control_response` que repete esse id. Os pedidos andam nos dois sentidos:

- **do host ao `claude`**: o `initialize`, o interrupt, a troca de modo, de modelo e de effort, as
  consultas;
- **do `claude` ao host**: o pedido de permissão (`can_use_tool`) e os pedidos de hook, de MCP e de
  elicitation, que este protocolo não atende.

## A forma

O pedido:

<!-- stdin -->
```json
{"type":"control_request","request_id":"mode-0","request":{"subtype":"set_permission_mode","mode":"acceptEdits"}}
```

A resposta de sucesso, com o corpo em `response.response` (quando tem corpo):

<!-- fixture: mode-and-model.stream.ndjson:2 -->
```json
{"type": "control_response", "response": {"subtype": "success", "request_id": "mode-0", "response": {"mode": "acceptEdits"}}}
```

A resposta de erro, com o texto em `response.error`:

<!-- stdin -->
```json
{"type":"control_response","response":{"subtype":"error","request_id":"…","error":"not supported"}}
```

- **O `request_id` é de quem pede.** O host escolhe o id dos pedidos dele; o `claude` escolhe o dos
  dele. A lib gera um id novo por pedido (um GUID de 32 dígitos, sem hífens): quem chama não o
  escolhe. O gravador usa ids legíveis (`init-1`, `mode-0`), e é por isso que eles aparecem nas
  gravações.
- **A resposta não diz qual pedido respondeu, só o id.** Quem pediu guarda o pedido pelo id para
  saber o que a resposta confirma. Na lib, a `ClaudeSession` faz isso e entrega o par como
  `control_replied`.
- **A ordem não é garantida.** Uma resposta pode chegar no meio das linhas de um turno.

## Do host ao `claude`

| `subtype` | O que pede | Corpo do pedido | Corpo da resposta | Gravado |
|---|---|---|---|---|
| `initialize` | O aperto de mão; os SDKs oficiais mandam primeiro | — | `commands`, `models`, `account`, `auth_conflict` | todas |
| `interrupt` | Para o turno | — | `still_queued`: as mensagens que ainda esperam a vez | `interrupted` |
| `set_permission_mode` | Troca o modo | `mode` | `mode` | `mode-and-model`, `plan*` |
| `set_model` | Troca o modelo | `model` | `model`, já resolvido | `mode-and-model`, `effort` |
| `apply_flag_settings` | Troca o effort | `settings.effortLevel` (texto ou `null`) | nenhum | `effort` |
| `get_settings` | Os ajustes em vigor | — | `applied` (`model`, `effort`) | `effort` |
| `get_context_usage` | O uso da janela de contexto | — | `categories`, `totalTokens`, `maxTokens`, `percentage` | `commands` |
| `get_task_output` | O que uma tarefa em segundo plano imprimiu | `task_id` | — | não |
| `remote_control` | Liga ou desliga o Remote Control | `enabled` | `session_url`, ao ligar | não |
| `get_chrome_dialog` | O estado da extensão do navegador | — | — | não |

### `initialize`

<!-- stdin -->
```json
{"type":"control_request","request_id":"init-1","request":{"subtype":"initialize"}}
```

A resposta lista os comandos de barra da sessão, os modelos e a conta:

<!-- fixture: edit.stream.ndjson:1 -->
```json
{"type": "control_response", "response": {"subtype": "success", "request_id": "init-1", "response": {"commands": [{"name": "compact", "description": "Free up context by summarizing the conversation so far", "argumentHint": "<optional custom summarization instructions>", "builtin": true}, "…"]}}}
```

- `commands[]`: `name` (sem a barra), `description`, `argumentHint`.
- `models[]`: `value`, `resolvedModel`, `displayName`, `description` e `supportedEffortLevels`.
- `account`: o login (`apiProvider`, `tokenSource`, `apiKeySource`), `email`, `organization` e
  `subscriptionType`. As gravações não têm: o gravador tira, porque é da conta de quem gravou.
- `auth_conflict`: quando há um token e uma API key ao mesmo tempo, os nomes das duas origens
  (`token_source` e `api_key_source`), nunca as credenciais.

Todas as gravações começam pela resposta do `initialize`, na linha 1, porque o gravador o manda
antes de qualquer mensagem. A linha `system/init` só chega com a primeira mensagem.

### `interrupt`

<!-- fixture: interrupted.stream.ndjson:22 -->
```json
{"type": "control_response", "response": {"subtype": "success", "request_id": "interrupt-1", "response": {"still_queued": []}}}
```

O turno interrompido termina com um `result` de `error_during_execution`. Um pedido de permissão
que estava aberto é cancelado antes (ver [`control_cancel_request`](#control_cancel_request)).

### `set_permission_mode` e `set_model`

O modo ou o modelo valem a partir da resposta de sucesso. A resposta do `set_model` traz o modelo
resolvido: no cenário `effort`, o pedido de `haiku` volta como `claude-haiku-5-5`.

<!-- fixture: effort.stream.ndjson:14 -->
```json
{"type": "control_response", "response": {"subtype": "success", "request_id": "model-3", "response": {"model": "claude-haiku-5-5"}}}
```

Os modos que a lib aceita pedir são `plan`, `default`, `acceptEdits` e `auto`. Um modo fora dessa
lista não é escrito (ver [Transporte](../transporte/#a-conferência)).

### `apply_flag_settings` e `get_settings`

<!-- stdin -->
```json
{"type":"control_request","request_id":"effort-1","request":{"subtype":"apply_flag_settings","settings":{"effortLevel":"high"}}}
```

A resposta não confirma nada: é um sucesso sem corpo. O effort em vigor vem na resposta do
`get_settings`:

<!-- fixture: effort.stream.ndjson:2 -->
```json
{"type": "control_response", "response": {"subtype": "success", "request_id": "effort-1"}}
```

<!-- fixture: effort.stream.ndjson:3 -->
```json
{"type": "control_response", "response": {"subtype": "success", "request_id": "settings-1", "response": {"applied": {"model": "claude-haiku-5-5", "effort": "high"}}}}
```

`effortLevel: null` volta ao effort padrão do modelo. Desde a 2.1.325, um valor inválido é
recusado com uma resposta de erro.

### `get_context_usage`

<!-- fixture: commands.stream.ndjson:12 -->
```json
{"type": "control_response", "response": {"subtype": "success", "request_id": "context-2", "response": {"categories": [{"name": "System prompt", "tokens": 3129, "kind": "used"}, "…"], "totalTokens": 25341, "maxTokens": 1000000, "percentage": 3}}}
```

`kind` é `used`, `deferred`, `buffer` ou `free`. Quando vêm, `model`, `memoryFiles` e `agents`
completam a resposta.

### Como o corpo é lido

A lib não sabe a que pedido uma resposta responde: ela lê o corpo pela forma.

| Se o corpo tem | Ela lê |
|---|---|
| `commands` | os comandos de barra; e, se tem também `models` ou `account`, o `auth_conflict` |
| `models` | os modelos |
| `applied` | os ajustes em vigor |
| `categories` e `totalTokens` | o uso do contexto |
| `account` | a conta |
| `rate_limits` | os limites de uso (a resposta de um `get_usage`, que a lib não pede mas lê) |
| `session_url` | o link do Remote Control |

## Do `claude` ao host

### `can_use_tool`, o pedido de permissão

Com `--permission-prompt-tool stdio`, cada ferramenta que precisa de permissão vira um pedido no
stdout, e o `claude` espera a resposta no stdin:

<!-- fixture: edit.stream.ndjson:40 -->
```json
{"type": "control_request", "request_id": "0f8155c2-5135-4e0b-9da0-4cbc9d41ee1e", "request": {"subtype": "can_use_tool", "tool_name": "Edit", "display_name": "Edit", "input": {"file_path": "C:\\demo\\app\\notes.md", "old_string": "line three\n", "new_string": "line 3 (edited)\n", "replace_all": false}, "description": "notes.md", "permission_suggestions": [{"type": "setMode", "mode": "acceptEdits", "destination": "session"}], "tool_use_id": "toolu_01FPyYmYbFzFZR24ZJtHzcWB"}}
```

| Campo | O que é |
|---|---|
| `tool_name` | A ferramenta. Sem ele, a lib não gera evento, e o transporte responde com erro |
| `display_name`, `description` | Como mostrar o pedido |
| `input` | O que a ferramenta vai fazer, como o modelo pediu |
| `permission_suggestions` | As regras que o `claude` sugere guardar para não perguntar de novo |
| `tool_use_id` | A chamada de ferramenta do pedido |
| `agent_id` | O sub-agent que pede (o `task_id` dele); ausente para a conversa principal |
| `decision_reason`, `decision_reason_type`, `decision_reason_code` | Por que o `claude` pergunta: o texto de um hook, de uma conferência de segurança… O tipo é `hook`, `safetyCheck`, `subcommandResults`, `other`… |
| `default_to_no` | O `claude` pede para o "não" vir selecionado |
| `suppress_always_allow_rule` | O `claude` não oferece regra para guardar: sem "permitir sempre" |
| `mcp_server.source` | De onde vem o servidor MCP da ferramenta; `claudeai` para um conector do claude.ai |

Nas gravações aparecem pedidos de `Edit`, `Write`, `Bash`, `PowerShell`, `ExitPlanMode` e
`AskUserQuestion`. O de um sub-agent tem `agent_id` (`subagent.stream.ndjson`, linha 50); o de um
hook, `decision_reason_type: "hook"` (`hook-ask`, linha 20); o de uma pasta protegida,
`decision_reason_type: "safetyCheck"` (`seguranca`, linha 20).

### A resposta de permissão

Três decisões, numa `control_response` de `success` com o `request_id` do pedido:

**Permitir**: o `input` volta como veio, em `updatedInput`.

<!-- stdin -->
```json
{"type":"control_response","response":{"subtype":"success","request_id":"0f8155c2-5135-4e0b-9da0-4cbc9d41ee1e","response":{"behavior":"allow","updatedInput":{"file_path":"C:\\demo\\app\\notes.md","old_string":"line three\n","new_string":"line 3 (edited)\n","replace_all":false}}}}
```

**Permitir sempre**: o mesmo, mais as `permission_suggestions` do pedido em `updatedPermissions`,
quando são uma lista.

<!-- stdin -->
```json
{"type":"control_response","response":{"subtype":"success","request_id":"…","response":{"behavior":"allow","updatedInput":{"…":"…"},"updatedPermissions":[{"type":"setMode","mode":"acceptEdits","destination":"session"}]}}}
```

**Negar**: com a mensagem para o modelo. Sem mensagem, a lib manda
`"The user denied this tool use."`.

<!-- stdin -->
```json
{"type":"control_response","response":{"subtype":"success","request_id":"…","response":{"behavior":"deny","message":"Do not create files, just explain."}}}
```

A mensagem chega ao modelo como o texto de um `tool_result` com `is_error: true`. No cenário
`denied`, o modelo lê a mensagem e explica no `result` que não criou o arquivo.

:::note[Sair do modo plan]
O fim de um plano é um pedido de permissão de `ExitPlanMode`. Para sair do plan e seguir com o
plano, o gravador manda primeiro um `set_permission_mode` para o modo seguinte (`acceptEdits` no
cenário `plan`, `auto` no `plan-auto`) e depois permite. Negar com uma mensagem devolve o plano ao
modelo com essa mensagem (cenário `plan-feedback`).
:::

### A regra da resposta de permissão

> A resposta a um pedido de permissão leva sempre o `input` e as `permission_suggestions` que o
> próprio lado do processo recebeu do `claude`. Quem chama só escolhe o `request_id`, a decisão e a
> mensagem.

É uma regra da lib, não do `claude`: ele aceita o `updatedInput` que receber, e é para isso que o
campo existe. Mas quem tem o stdin de um `claude` não deve executar um `input` que veio de outro
lugar. Se o pedido passa por uma tela, por uma rede ou por outro processo, quem responde poderia
trocar `"command": "ls"` por outro comando, e o `claude` rodaria o comando trocado com a permissão
dada ao primeiro.

Na lib:

- **o comando de resposta (`AnswerPermission`) não tem campo para o `input`** nem para as
  sugestões: só o id, a decisão e a mensagem;
- **quem escreve no stdin monta a resposta com o pedido que ele mesmo leu** do stdout, guardado num
  livro de pedidos (`PermissionLedger`);
- **cada pedido é respondido uma vez.** Responder tira o pedido do livro. Ele também sai quando o
  `claude` o cancela, quando chega o `tool_result` da chamada dele e quando o turno termina. Um id
  que não está pendente (já respondido, cancelado, resolvido ou desconhecido) não escreve nada.

Desde a 2.1.321, o `AskUserQuestion` nega a resposta de um host que mexe no `input` (campo
`afkTimeoutMs`). Com a regra, isso não acontece.

:::caution[Ainda não conferido]
Com `--permission-prompt-tool`, um resultado grande de "allow" é gravado em arquivo pelo
tratamento de saída grande do MCP ([#109249](https://github.com/anthropics/claude-code/issues/109249)).
A resposta devolve o `input` inteiro, então um `Write` de arquivo grande passa por aí. Falta ver
com um `claude` de verdade.
:::

### `control_cancel_request`

O `claude` desiste de um pedido de permissão que ainda não teve resposta:

<!-- fixture: interrupted.stream.ndjson:21 -->
```json
{"type": "control_cancel_request", "request_id": "dbfbeade-6899-46b2-ba43-120f8dea7acd"}
```

No cenário `interrupted`, o pedido de `PowerShell` da linha 20 é cancelado na linha 21, pelo
interrupt. Depois do cancelamento, o pedido sai do livro, e uma resposta a ele não escreve nada.

### O pedido que ninguém atende

O `claude` também pode mandar pedidos de controle de outros tipos: o callback de um hook do SDK,
uma mensagem para um servidor MCP do SDK, uma elicitation. Sem resposta, ele espera para sempre.
O transporte da lib responde a qualquer um deles com erro:

<!-- stdin -->
```json
{"type":"control_response","response":{"subtype":"error","request_id":"…","error":"not supported"}}
```

E um `can_use_tool` que o leitor não consegue ler (sem `tool_name`, por exemplo), e que por isso
nunca chegaria a quem responde, recebe o erro `"malformed permission request"`.
