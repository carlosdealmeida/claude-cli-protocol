---
title: Eventos
description: La tabla de los eventos que producen los lectores — de qué línea sale cada uno, los campos, y cuáles no ejercita ninguna grabación.
---

Un lector convierte cada línea en ninguno, uno o más eventos tipados. Esta página es su referencia:
el nombre, la línea de la que sale y los campos. **Los nombres son los del formato del resultado
esperado** (ver [Fixtures](../fixtures/#el-resultado-esperado)), en `snake_case`, y no los del
stream: el `cwd` de la línea `system/init` es el `working_directory` del `session_started`. Una
biblioteca en otro lenguaje puede dar otros nombres a sus tipos, pero el resultado esperado que
produce usa estos.

Reglas que valen para todos:

- **El conjunto de campos es fijo.** Un campo sin valor va `null`; no desaparece.
- **Los valores que se pasan tal cual** (el `input` de un `tool_use` y de un `permission_requested`,
  las `suggestions`, el `tool_use_result` y el `response` de un `control_acknowledged`) van como
  `claude` los mandó, con sus nombres de clave.
- **Lector en vivo y lector del archivo.** "stdout" quiere decir el lector de las líneas de
  `claude -p`; "archivo" quiere decir el lector del archivo de la sesión (`.jsonl`). Algunos eventos
  salen de los dos. La columna "Sale de" dice dónde aparece de hecho la línea: las líneas `system` de
  tarea, de hook, de `api_retry`, de `thinking_tokens`, de `commands_changed`, de `bridge_state` y de
  `local_command` las lee el mismo código en los dos lectores, y dan evento en el archivo también
  cuando el archivo las tiene. Las de `init`, `status`, `per_turn_effort_changed`,
  `model_refusal_fallback` y `peer_message_hold` son solo del stdout.

## La sesión y el turno

| Evento | Sale de | Campos |
|---|---|---|
| `session_started` | stdout: `system/init` | `session_id`, `model`, `working_directory`, `permission_mode`, `claude_version`, `terminal_commands`, `fast_mode_state`, `ultrathink_available` |
| `status_changed` | stdout: `system/status` | `status`, `permission_mode` |
| `turn_completed` | stdout: `result` | `subtype`, `is_error`, `duration_ms`, `cost_usd`, `input_tokens`, `output_tokens`, `api_duration_ms`, `error_message`, `fast_mode_state` |
| `system_notice` | stdout y archivo: `system/compact_boundary`, `system/api_retry` | `subtype`, `trigger`, `pre_tokens`, `post_tokens`, `error_message` |
| `informational` | stdout: `system/informational` | `level`, `text` |
| `thinking_tokens` | stdout: `system/thinking_tokens` | `estimated_tokens` |
| `prompt_cache_used` | stdout y archivo: el `usage` de una línea `assistant` de la conversación principal | `ttl`, `at` |

- `turn_completed.input_tokens` suma `input_tokens`, `cache_creation_input_tokens` y
  `cache_read_input_tokens`. `cost_usd` es el `total_cost_usd`.
- `prompt_cache_used.ttl` es 1 h o 5 min, por el `cache_creation` (`ephemeral_1h_input_tokens` o
  `ephemeral_5m_input_tokens`); `null` cuando la respuesta solo leyó la caché.
- `informational.level` es `info`, `warning` o `error` (`info` cuando la línea no lo dice). Un hook
  que bloqueó el prompt manda un `warning` (escenario `hook-blocked`).

## La respuesta mientras llega

| Evento | Sale de | Campos |
|---|---|---|
| `message_started` | stdout: `stream_event` / `message_start` | `message_id`, `parent_tool_use_id` |
| `block_started` | stdout: `stream_event` / `content_block_start` | `message_id`, `index`, `parent_tool_use_id`, `block` |
| `block_delta` | stdout: `stream_event` / `content_block_delta` | `message_id`, `index`, `parent_tool_use_id`, `kind`, `text`, `estimated_tokens` |
| `block_stopped` | stdout: `stream_event` / `content_block_stop` | `message_id`, `index`, `parent_tool_use_id` |

- El `message_id` de los tres últimos es el del último `message_start` del mismo
  `parent_tool_use_id`; vacío si no hubo ninguno.
- `block_delta.kind` es `text`, `thinking` o `input_json`.
- El `block` de un `block_started` de `tool_use` tiene `input` vacío (`{}`).

## Los mensajes

| Evento | Sale de | Campos |
|---|---|---|
| `assistant_block` | stdout y archivo: `assistant`, uno por bloque | `message_id`, `parent_tool_use_id`, `block`, `timestamp`, `thinking_duration_ms`, `uuid`, `supersedes`, `is_synthetic` |
| `user_block` | stdout y archivo: `user` (y `attachment` de `queued_command`, en el archivo) | `parent_tool_use_id`, `block`, `tool_use_result`, `is_meta`, `is_compact_summary`, `timestamp`, `images`, `is_synthetic`, `origin`, `uuid`, `is_queued_command` |
| `command_echo` | archivo: el texto `<command-name>` de una línea `user` | `name`, `arguments` |
| `command_output` | stdout y archivo: el texto `<local-command-stdout>`; `system/local_command` | `text` |

- `assistant_block.is_synthetic` es verdadero cuando el `message.model` es `<synthetic>`.
- `assistant_block.supersedes` son los `uuid` de las líneas que esta reemplaza (una respuesta que
  `claude` descartó y escribió de nuevo).
- `user_block.origin` dice de dónde vino el mensaje: `kind` (`human`, `peer`, `channel`,
  `task-notification`, `coordinator`, `plugin`…), `name`, `sender_task_id` y `subkind`.
- `command_echo.name` viene sin la barra.

### Los bloques

El campo `block` es un objeto con `kind` y los campos del bloque:

| `kind` | Campos |
|---|---|
| `text` | `text` |
| `thinking` | `text` |
| `tool_use` | `id`, `name`, `input` |
| `tool_result` | `tool_use_id`, `text`, `is_error`, `non_execution_kind`, `images` |
| `image` | `media_type`, `data` |

El `text` de un `tool_result` junta los bloques de texto del `content` con `\n`. El `data` de una
imagen va como `{"length": …, "sha256": …}` en el resultado esperado, no entero.

## Permiso y control

| Evento | Sale de | Campos |
|---|---|---|
| `permission_requested` | stdout: `control_request` / `can_use_tool` | `request_id`, `tool_name`, `display_name`, `description`, `tool_use_id`, `input`, `suggestions`, `agent_id`, `default_to_no`, `suppress_always_allow_rule`, `decision_reason`, `decision_reason_type`, `decision_reason_code`, `mcp_server_source` |
| `permission_cancelled` | stdout: `control_cancel_request` | `request_id` |
| `control_acknowledged` | stdout: `control_response` | `request_id`, `is_error`, `response`, `error` |
| `commands_available` | stdout: el cuerpo de una `control_response` con `commands`; `system/commands_changed` | `commands` |
| `models_available` | stdout: el cuerpo de una `control_response` con `models` | `models` |
| `auth_conflict_reported` | stdout: el cuerpo de una `control_response` con `commands` y `models` o `account` | `conflict` |
| `settings_applied` | stdout: el cuerpo de una `control_response` con `applied` | `model`, `effort`, `has_effort`, `ultrathink_available` |
| `context_usage_reported` | stdout: el cuerpo de una `control_response` con `categories` y `totalTokens` | `usage` |

- Una `control_response` da un `control_acknowledged` y, después de él, los eventos que tenga el
  cuerpo, en el orden de la tabla de [Control](../controle/#cómo-se-lee-el-cuerpo).
- `commands[]`: `name`, `description`, `argument_hint`.
- `models[]`: `value`, `resolved_model`, `display_name`, `description`, `effort_levels`.
- `usage`: `total_tokens`, `max_tokens`, `percentage` (entero, redondeado), `categories[]` (`name`,
  `tokens`, `kind`), `model`, `memory_files[]` (`path`, `tokens`) y `agents[]` (`name`, `tokens`).
- `conflict`: `token_source` y `api_key_source`, o `null` cuando no hay conflicto.
- `settings_applied.has_effort` es falso cuando el `applied` no tiene `effort`.

## Las tareas en segundo plano

| Evento | Sale de | Campos |
|---|---|---|
| `agent_task_started` | stdout: `system/task_started` | `task_id`, `tool_use_id`, `description`, `agent_type`, `is_background`, `depth`, `task_type`, `prompt` |
| `agent_task_progress` | stdout: `system/task_progress` | `task_id`, `tool_use_id`, `activity`, `tokens`, `tool_uses`, `duration_ms` |
| `agent_task_updated` | stdout: `system/task_updated` | `task_id`, `status`, `is_backgrounded` |
| `agent_task_finished` | stdout: `system/task_notification`; archivo: el texto `<task-notification>` de una línea `user` | `task_id`, `tool_use_id`, `status`, `summary`, `tokens`, `tool_uses`, `duration_ms` |

- `task_type` es `local_agent` para un subagente y `local_bash` para un comando.
- `depth` es el `spawn_depth` (1 cuando la línea no lo dice).
- `tokens`, `tool_uses` y `duration_ms` vienen del `usage` de la línea.

## Los que ninguna grabación ejercita

Estos eventos salen de líneas que las 42 grabaciones no tienen. Su forma está en el código de la
biblioteca .NET y en las pruebas unitarias, y es la que está aquí. Cuando una grabación los traiga,
esta tabla se comprueba contra ella.

| Evento | Sale de | Campos |
|---|---|---|
| `hook_started` | stdout y archivo: `system/hook_started` | `hook_id`, `hook_event` |
| `hook_finished` | stdout y archivo: `system/hook_response` | `hook_id` |
| `command_lifecycle` | stdout: `command_lifecycle` | `command_uuid`, `state`, `ran_ahead` |
| `per_turn_effort_changed` | stdout: `system/per_turn_effort_changed` | ninguno |
| `usage_limits_reported` | stdout: `rate_limit_event`; el cuerpo de una `control_response` con `rate_limits` | `limits` |
| `account_reported` | stdout: el cuerpo de una `control_response` con `account` | `account` |
| `remote_control_changed` | stdout y archivo: `system/bridge_state`; el cuerpo de una `control_response` con `session_url` | `state`, `url`, `detail` |
| `model_refusal_fallback` | stdout: `system/model_refusal_fallback` | `original_model`, `fallback_model`, `category` |
| `peer_message_hold` | stdout: `system/peer_message_hold` | `state`, `from`, `message_uuid`, `from_name`, `cause`, `outcome` |

- `hook_started.hook_event` es el evento que el hook atiende: `SessionStart`, `Setup`,
  `PreToolUse`…
- `command_lifecycle.state` es `queued`, `started`, `completed`, `cancelled`, `discarded` o
  `refused` (ver [Comandos](../comandos/#con-uuid)).
- `per_turn_effort_changed` avisa que la palabra `ultrathink` en un prompt subió el effort del turno.
- `limits`: `windows[]` (`name`, `utilization`, `resets_at`, `model`) e `is_snapshot`. Un
  `rate_limit_event` trae solo las ventanas que cambiaron, con `utilization` de 0 a 1 y `resetsAt`
  en segundos Unix; la respuesta de un `get_usage` es una foto entera (`is_snapshot` verdadero).
- `account`: `auth_method` (`claudeai`, `api-key`, `bedrock`, `vertex`, `foundry`, `gateway` o
  `third-party`), `email`, `organization` y `plan` (el nombre del plan, como "Max").
- `model_refusal_fallback`: las salvaguardas de `claude` cambiaron el modelo, y la respuesta viene
  del `fallback_model`.
- `peer_message_hold.state` es `held`, `released` o `dropped`: un mensaje de otra sesión que `claude`
  retuvo para que el usuario lo revise.

## El evento de la sesión

| Evento | Sale de | Campos |
|---|---|---|
| `control_replied` | `ClaudeSession`, no un lector: la respuesta a una solicitud que ella mandó | `command`, `reply` |

Junta la solicitud que la sesión mandó (`command`) con el `control_acknowledged` que la respondió
(`reply`), por el `request_id`. No aparece en el resultado esperado, porque no sale de una línea.

## Líneas que no dan evento

- `stream_event` de `message_delta` y de `message_stop`;
- `system/background_tasks_changed`;
- `system/model_refusal_no_fallback`;
- cualquier `control_request` que no sea `can_use_tool` (el transporte responde con error, ver
  [Control](../controle/#la-solicitud-que-nadie-atiende));
- toda línea de `type` o `subtype` desconocido.
