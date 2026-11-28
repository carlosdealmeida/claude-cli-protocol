---
title: Eventos
description: A tabela dos eventos que os leitores produzem — de que linha cada um sai, os campos, e quais nenhuma gravação exercita.
---

Um leitor transforma cada linha em nenhum, um ou mais eventos tipados. Esta página é a referência
deles: o nome, a linha de que sai e os campos. **Os nomes são os do formato do resultado esperado**
(ver [Fixtures](../fixtures/#o-resultado-esperado)), em `snake_case`, e não os do stream: o `cwd` da
linha `system/init` é o `working_directory` do `session_started`. Uma lib em outra linguagem pode dar
outros nomes aos seus tipos, mas o resultado esperado que ela produz usa estes.

Regras que valem para todos:

- **O conjunto de campos é fixo.** Um campo sem valor vai `null`; ele não some.
- **Os valores repassados** (o `input` de um `tool_use` e de um `permission_requested`, as
  `suggestions`, o `tool_use_result` e o `response` de um `control_acknowledged`) vão como o `claude`
  mandou, com os nomes de chave dele.
- **Leitor ao vivo e leitor do arquivo.** "stdout" quer dizer o leitor das linhas do
  `claude -p`; "arquivo" quer dizer o leitor do arquivo da sessão (`.jsonl`). Alguns eventos saem
  dos dois. A coluna "Sai de" diz onde a linha aparece de fato: as linhas `system` de tarefa, de
  hook, de `api_retry`, de `thinking_tokens`, de `commands_changed`, de `bridge_state` e de
  `local_command` são lidas pelo mesmo código nos dois leitores, e dão evento no arquivo também
  quando o arquivo as tem. As de `init`, `status`, `per_turn_effort_changed`,
  `model_refusal_fallback` e `peer_message_hold` são só do stdout.

## A sessão e o turno

| Evento | Sai de | Campos |
|---|---|---|
| `session_started` | stdout: `system/init` | `session_id`, `model`, `working_directory`, `permission_mode`, `claude_version`, `terminal_commands`, `fast_mode_state`, `ultrathink_available` |
| `status_changed` | stdout: `system/status` | `status`, `permission_mode` |
| `turn_completed` | stdout: `result` | `subtype`, `is_error`, `duration_ms`, `cost_usd`, `input_tokens`, `output_tokens`, `api_duration_ms`, `error_message`, `fast_mode_state` |
| `system_notice` | stdout e arquivo: `system/compact_boundary`, `system/api_retry` | `subtype`, `trigger`, `pre_tokens`, `post_tokens`, `error_message` |
| `informational` | stdout: `system/informational` | `level`, `text` |
| `thinking_tokens` | stdout: `system/thinking_tokens` | `estimated_tokens` |
| `prompt_cache_used` | stdout e arquivo: o `usage` de uma linha `assistant` da conversa principal | `ttl`, `at` |

- `turn_completed.input_tokens` soma `input_tokens`, `cache_creation_input_tokens` e
  `cache_read_input_tokens`. `cost_usd` é o `total_cost_usd`.
- `prompt_cache_used.ttl` é 1 h ou 5 min, pelo `cache_creation` (`ephemeral_1h_input_tokens` ou
  `ephemeral_5m_input_tokens`); `null` quando a resposta só leu o cache.
- `informational.level` é `info`, `warning` ou `error` (`info` quando a linha não diz). Um hook que
  bloqueou o prompt manda um `warning` (cenário `hook-blocked`).

## A resposta enquanto chega

| Evento | Sai de | Campos |
|---|---|---|
| `message_started` | stdout: `stream_event` / `message_start` | `message_id`, `parent_tool_use_id` |
| `block_started` | stdout: `stream_event` / `content_block_start` | `message_id`, `index`, `parent_tool_use_id`, `block` |
| `block_delta` | stdout: `stream_event` / `content_block_delta` | `message_id`, `index`, `parent_tool_use_id`, `kind`, `text`, `estimated_tokens` |
| `block_stopped` | stdout: `stream_event` / `content_block_stop` | `message_id`, `index`, `parent_tool_use_id` |

- `message_id` dos três últimos é o do último `message_start` do mesmo `parent_tool_use_id`; vazio
  se não houve nenhum.
- `block_delta.kind` é `text`, `thinking` ou `input_json`.
- O `block` de um `block_started` de `tool_use` tem `input` vazio (`{}`).

## As mensagens

| Evento | Sai de | Campos |
|---|---|---|
| `assistant_block` | stdout e arquivo: `assistant`, um por bloco | `message_id`, `parent_tool_use_id`, `block`, `timestamp`, `thinking_duration_ms`, `uuid`, `supersedes`, `is_synthetic` |
| `user_block` | stdout e arquivo: `user` (e `attachment` de `queued_command`, no arquivo) | `parent_tool_use_id`, `block`, `tool_use_result`, `is_meta`, `is_compact_summary`, `timestamp`, `images`, `is_synthetic`, `origin`, `uuid`, `is_queued_command`, `context` |
| `command_echo` | arquivo: o texto `<command-name>` de uma linha `user` | `name`, `arguments` |
| `command_output` | stdout e arquivo: o texto `<local-command-stdout>`; `system/local_command` | `text` |

- `assistant_block.is_synthetic` é verdadeiro quando o `message.model` é `<synthetic>`.
- `assistant_block.supersedes` são os `uuid` das linhas que esta substitui (uma resposta que o
  `claude` descartou e escreveu de novo).
- `user_block.origin` diz de onde veio a mensagem: `kind` (`human`, `peer`, `channel`,
  `task-notification`, `coordinator`, `plugin`…), `name`, `sender_task_id` e `subkind`.
- `user_block.context` são os blocos de contexto que a IDE manda antes do texto: a saída de um
  terminal, uma aba do navegador, as instruções do navegador ou uma seleção do editor. Cada um tem
  `kind` (`terminal`, `browser-tab`, `browser-instructions` ou `selection`), `subject` e `content`.
  O leitor tira esses blocos do texto, então é só aqui que eles aparecem. Nenhuma gravação tem um:
  a lista vem vazia em todas.
- `command_echo.name` vem sem a barra.

### Os blocos

O campo `block` é um objeto com `kind` e os campos do bloco:

| `kind` | Campos |
|---|---|
| `text` | `text` |
| `thinking` | `text` |
| `tool_use` | `id`, `name`, `input` |
| `tool_result` | `tool_use_id`, `text`, `is_error`, `non_execution_kind`, `images` |
| `image` | `media_type`, `data` |
| `advisor_use` | `id` |
| `advisor_result` | `tool_use_id`, `outcome`, `text`, `error_code` |

O `text` de um `tool_result` junta os blocos de texto do `content` com `\n`. O `data` de uma imagem
vai como `{"length": …, "sha256": …}` no resultado esperado, não inteiro.

Os dois últimos são do advisor, uma ferramenta do servidor (cenário `advisor`). `advisor_use` é a
chamada: um bloco `server_tool_use` de nome `advisor`. `advisor_result` é o veredito, um bloco
`advisor_tool_result` que vem na mensagem do assistente, não numa linha `user`. `outcome` é
`reviewed`, `declined` (o `stop_reason` do veredito é `refusal`) ou `unavailable` (com o
`error_code`). `text` é `null` quando o veredito vem cifrado ou não vem.

## Permissão e controle

| Evento | Sai de | Campos |
|---|---|---|
| `permission_requested` | stdout: `control_request` / `can_use_tool` | `request_id`, `tool_name`, `display_name`, `description`, `tool_use_id`, `input`, `suggestions`, `agent_id`, `default_to_no`, `suppress_always_allow_rule`, `decision_reason`, `decision_reason_type`, `decision_reason_code`, `mcp_server_source` |
| `permission_cancelled` | stdout: `control_cancel_request` | `request_id` |
| `control_acknowledged` | stdout: `control_response` | `request_id`, `is_error`, `response`, `error` |
| `commands_available` | stdout: o corpo de uma `control_response` com `commands`; `system/commands_changed` | `commands` |
| `models_available` | stdout: o corpo de uma `control_response` com `models` | `models` |
| `auth_conflict_reported` | stdout: o corpo de uma `control_response` com `commands` e `models` ou `account` | `conflict` |
| `settings_applied` | stdout: o corpo de uma `control_response` com `applied` | `model`, `effort`, `has_effort`, `ultrathink_available` |
| `context_usage_reported` | stdout: o corpo de uma `control_response` com `categories` e `totalTokens` | `usage` |

- Uma `control_response` dá um `control_acknowledged` e, depois dele, os eventos que o corpo tiver,
  na ordem da tabela de [Controle](../controle/#como-o-corpo-é-lido).
- `commands[]`: `name`, `description`, `argument_hint`.
- `models[]`: `value`, `resolved_model`, `display_name`, `description`, `effort_levels`.
- `usage`: `total_tokens`, `max_tokens`, `percentage` (inteiro, arredondado), `categories[]`
  (`name`, `tokens`, `kind`), `model`, `memory_files[]` (`path`, `tokens`) e `agents[]` (`name`,
  `tokens`).
- `conflict`: `token_source` e `api_key_source`, ou `null` quando não há conflito.
- `settings_applied.has_effort` é falso quando o `applied` não tem `effort`.

## As tarefas em segundo plano

| Evento | Sai de | Campos |
|---|---|---|
| `agent_task_started` | stdout: `system/task_started` | `task_id`, `tool_use_id`, `description`, `agent_type`, `is_background`, `depth`, `task_type`, `prompt` |
| `agent_task_progress` | stdout: `system/task_progress` | `task_id`, `tool_use_id`, `activity`, `tokens`, `tool_uses`, `duration_ms` |
| `agent_task_updated` | stdout: `system/task_updated` | `task_id`, `status`, `is_backgrounded` |
| `agent_task_finished` | stdout: `system/task_notification`; arquivo: o texto `<task-notification>` de uma linha `user` | `task_id`, `tool_use_id`, `status`, `summary`, `tokens`, `tool_uses`, `duration_ms` |
| `background_tasks_changed` | stdout: `system/background_tasks_changed` | `tasks` |

- `task_type` é `local_agent` para um sub-agent, `local_bash` para um comando e `local_workflow`
  para um workflow.
- `depth` é o `spawn_depth` (1 quando a linha não diz).
- `tokens`, `tool_uses` e `duration_ms` vêm do `usage` da linha.
- `background_tasks_changed.tasks[]`: `task_id`, `task_type` e `description`. Cada linha traz a
  lista inteira e substitui a anterior; uma lista vazia quer dizer que nada roda. Uma entrada sem
  `task_id` é pulada. O arquivo da sessão nunca tem essa linha.

## Os que nenhuma gravação exercita

Estes eventos saem de linhas que as 50 gravações não têm. A forma deles está no código da lib
.NET e nos testes de unidade, e é o que está aqui. Quando uma gravação os trouxer, esta tabela é
conferida contra ela.

| Evento | Sai de | Campos |
|---|---|---|
| `hook_started` | stdout e arquivo: `system/hook_started` | `hook_id`, `hook_event` |
| `hook_finished` | stdout e arquivo: `system/hook_response` | `hook_id` |
| `command_lifecycle` | stdout: `command_lifecycle` | `command_uuid`, `state`, `ran_ahead` |
| `per_turn_effort_changed` | stdout: `system/per_turn_effort_changed` | nenhum |
| `usage_limits_reported` | stdout: `rate_limit_event`; o corpo de uma `control_response` com `rate_limits` | `limits` |
| `account_reported` | stdout: o corpo de uma `control_response` com `account` | `account` |
| `remote_control_changed` | stdout e arquivo: `system/bridge_state`; o corpo de uma `control_response` com `session_url` | `state`, `url`, `detail` |
| `model_refusal_fallback` | stdout: `system/model_refusal_fallback` | `original_model`, `fallback_model`, `category` |
| `peer_message_hold` | stdout: `system/peer_message_hold` | `state`, `from`, `message_uuid`, `from_name`, `cause`, `outcome` |

- `hook_started.hook_event` é o evento que o hook atende: `SessionStart`, `Setup`, `PreToolUse`…
- `command_lifecycle.state` é `queued`, `started`, `completed`, `cancelled`, `discarded` ou
  `refused` (ver [Comandos](../comandos/#com-uuid)).
- `per_turn_effort_changed` avisa que a palavra `ultrathink` num prompt subiu o effort do turno.
- `limits`: `windows[]` (`name`, `utilization`, `resets_at`, `model`) e `is_snapshot`. Um
  `rate_limit_event` traz só as janelas que mudaram, com `utilization` de 0 a 1 e `resetsAt` em
  segundos Unix; a resposta de um `get_usage` é um retrato inteiro (`is_snapshot` verdadeiro).
- `account`: `auth_method` (`claudeai`, `api-key`, `bedrock`, `vertex`, `foundry`, `gateway` ou
  `third-party`), `email`, `organization` e `plan` (o nome do plano, como "Max").
- `model_refusal_fallback`: as salvaguardas do `claude` trocaram o modelo, e a resposta vem do
  `fallback_model`.
- `peer_message_hold.state` é `held`, `released` ou `dropped`: uma mensagem de outra sessão que o
  `claude` segurou para o usuário revisar.

## O evento da sessão

| Evento | Sai de | Campos |
|---|---|---|
| `control_replied` | a `ClaudeSession`, não um leitor: a resposta a um pedido que ela mandou | `command`, `reply` |

Ele junta o pedido que a sessão mandou (`command`) ao `control_acknowledged` que o respondeu
(`reply`), pelo `request_id`. Não aparece no resultado esperado, porque não sai de uma linha.

## Linhas que não dão evento

- `stream_event` de `message_delta` e de `message_stop`;
- `system/model_refusal_no_fallback`;
- qualquer `control_request` que não seja `can_use_tool` (o transporte responde com erro, ver
  [Controle](../controle/#o-pedido-que-ninguém-atende));
- toda linha de `type` ou `subtype` desconhecido.
