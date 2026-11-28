---
title: Protocolo
description: Cómo se llama a claude -p, la forma de una línea, los tipos de línea del stdout y el turno.
---

`claude -p` con `--input-format stream-json` y `--output-format stream-json` es un proceso que lee
una línea JSON por comando en el stdin y escribe una línea JSON por evento en el stdout. Esta
página dice qué sale por el stdout. Lo que entra por el stdin está en [Comandos](../comandos/) y en
[Control](../controle/); el proceso, en [Transporte](../transporte/).

:::caution[El protocolo no tiene contrato]
Anthropic no documenta este protocolo como interfaz estable
([anthropics/claude-code#111046](https://github.com/anthropics/claude-code/issues/111046)). Lo que
está aquí se comprobó en las grabaciones de `fixtures/`, hechas con Claude Code 2.1.331 (el
escenario `image`, con 2.1.286; `advisor`, `bash-ansi`, `stop-task` y `workflow`, con 2.1.330), y en el código de la biblioteca .NET. Una versión nueva puede
cambiar cualquier línea. La regrabación semanal de las fixtures es lo que avisa.
:::

## La llamada

```
claude -p --input-format stream-json --output-format stream-json --verbose
       --include-partial-messages --permission-prompt-tool stdio --permission-mode <modo>
```

- `--verbose` es obligatorio: sin él, `claude -p` rechaza `--output-format stream-json`.
- `--include-partial-messages` hace que escriba los trozos de la respuesta mientras llega (las
  líneas `stream_event`).
- `--permission-prompt-tool stdio` hace que pida permiso por su propio stdout, con un
  `control_request` de `can_use_tool`, y espere la respuesta en el stdin.

Los opcionales (`--resume`, `--model`, `--effort`) y la comprobación de cada valor están en
[Transporte](../transporte/#los-argumentos).

## La línea

- **Una línea, un objeto JSON**, terminada en `\n`. El stdout es UTF-8.
- **El campo `type`** dice qué es la línea. En `system`, el `subtype` dice qué aviso; en
  `stream_event`, el `event.type`; en `control_request`, el `request.subtype`.
- **Casi toda línea trae `session_id` y `uuid`.** El `session_id` es el de la sesión; el `uuid`, el
  de la línea.
- **Un lector no lanza excepciones.** Línea en blanco, línea que no empieza por `{` (después de
  quitar espacios y códigos de terminal del principio), JSON inválido, `type` desconocido o campo
  que falta: la línea no genera evento, y la lectura sigue. Así lo hacen los dos lectores de la
  biblioteca .NET, y es lo que mantiene en pie una biblioteca cuando `claude` gana una línea nueva.
- **Un campo desconocido se ignora.** Las líneas traen muchos más campos de los que un lector usa
  (el `result` tiene más de veinte). Un lector lee los que conoce.

## Los tipos de línea

Recuento en las 25 grabaciones del stdout (`fixtures/*.stream.ndjson`):

| `type` | Subtipo | En las grabaciones | Qué es |
|---|---|---:|---|
| `system` | `init` | 33 | El comienzo de un turno: sesión, modelo, carpeta, modo, versión |
| `system` | `status` | 74 | Qué está haciendo `claude` (`requesting`, `compacting`, `null`) y el modo |
| `system` | `thinking_tokens` | 30 | Cuántos tokens ha pensado `claude` hasta ahora en el bloque que está llegando |
| `system` | `task_started`, `task_progress`, `task_updated`, `task_notification` | 15 | Una tarea en segundo plano: un subagente, un comando o un workflow |
| `system` | `compact_boundary` | 2 | La conversación se compactó |
| `system` | `informational` | 1 | Un aviso de `claude` sobre su propio trabajo (un hook que bloqueó el prompt, por ejemplo) |
| `system` | `background_tasks_changed` | 6 | La lista entera de las tareas en segundo plano, en cada cambio |
| `stream_event` | `message_start` | 61 | Una respuesta empezó a llegar |
| `stream_event` | `content_block_start`, `content_block_delta`, `content_block_stop` | 1.100 | Los bloques de la respuesta, trozo a trozo |
| `stream_event` | `message_delta`, `message_stop` | 122 | El fin de la respuesta. La biblioteca no los lee |
| `assistant` | | 97 | Un bloque entero de la respuesta |
| `user` | | 48 | Lo que vuelve al modelo: el resultado de una herramienta, un texto de `claude` |
| `control_request` | `can_use_tool` | 20 | Una solicitud de permiso |
| `control_cancel_request` | | 1 | `claude` desistió de una solicitud de permiso |
| `control_response` | `success` | 41 | La respuesta a una solicitud de control del host |
| `result` | `success`, `error_during_execution` | 33 | El fin de un turno |

Hay líneas que la biblioteca lee y que ninguna grabación tiene. En `system`: `api_retry`,
`hook_started`, `hook_response`, `commands_changed`, `bridge_state`, `local_command`,
`per_turn_effort_changed`, `model_refusal_fallback` y `peer_message_hold`. Fuera de `system`:
`rate_limit_event` (el grabador la quita, porque es de la cuenta de quien grabó) y
`command_lifecycle`. Los eventos que salen de ellas están en [Eventos](../eventos/), marcados como
no grabados.

## El comienzo del turno: `system/init`

Una línea `system/init` abre cada turno, no cada proceso. En las 25 grabaciones, los `system/init`
y los `result` se alternan, uno a uno.

<!-- fixture: edit.stream.ndjson:2 -->
```json
{"type": "system", "subtype": "init", "session_id": "8f8ab1b3-6d82-4572-9c2c-6f5f166d7bd2", "model": "claude-haiku-5-5", "cwd": "C:\\demo\\app", "permissionMode": "default", "claude_code_version": "2.1.331", "tools": ["Task", "AskUserQuestion", "Bash", "…"]}
```

Lo que un lector saca de ella: `session_id`, `model`, `cwd`, `permissionMode` y
`claude_code_version`; cuando vienen, `terminal_slash_commands`, `fast_mode_state` y
`ultrathink_available`. Las grabaciones guardan solo los campos conocidos de la línea: el grabador
quita el resto (ver [Fixtures](../fixtures/#la-sanitización)).

:::note
El modo que vale es el de esta línea, no el que se pidió en la línea de comandos. Desde la 2.1.323,
un `--permission-mode auto` empieza en `default` cuando el auto no está disponible
([#111343](https://github.com/anthropics/claude-code/issues/111343)).
:::

## La respuesta mientras llega: `stream_event`

Con `--include-partial-messages`, cada respuesta del modelo llega primero en trozos, en el formato
de streaming de la API de Anthropic, dentro de `event`:

<!-- fixture: edit.stream.ndjson:5 -->
```json
{"type": "stream_event", "event": {"type": "content_block_start", "index": 0, "content_block": {"type": "tool_use", "id": "toolu_015zZi1E7vQS17Zr1yMU1XbP", "name": "Read", "input": {}}}, "parent_tool_use_id": null}
```

<!-- fixture: edit.stream.ndjson:7 -->
```json
{"type": "stream_event", "event": {"type": "content_block_delta", "index": 0, "delta": {"type": "input_json_delta", "partial_json": "{\"file_"}}, "parent_tool_use_id": null}
```

- **`message_start`** trae `event.message.id`, el id de la respuesta. Los deltas que vienen después
  no traen ese id: **un lector guarda el id del último `message_start` de cada
  `parent_tool_use_id`** y lo pone en los eventos de los bloques. Por eso el lector del stdout tiene
  estado, y el del archivo de la sesión no.
- **`content_block_start`** abre el bloque `index`. Un `tool_use` empieza con `input` vacío (`{}`).
- **`content_block_delta`** extiende el bloque. El `delta.type` dice qué parte: `text_delta` (campo
  `text`), `thinking_delta` (campo `thinking`, y `estimated_tokens` cuando viene) o
  `input_json_delta` (campo `partial_json`, un trozo del JSON del `input`, que solo se cierra al
  final).
- **`content_block_stop`** cierra el bloque.
- **`message_delta`** y **`message_stop`** cierran la respuesta. La biblioteca no los lee: la línea
  `assistant` trae el bloque entero.
- **`parent_tool_use_id`** es la llamada a una herramienta del subagente que escribe la respuesta, o
  `null` para la conversación principal.

## El bloque entero: `assistant`

Después de los trozos, `claude` escribe la respuesta entera en líneas `assistant`, en el formato de
mensaje de la API: `message.id`, `message.content` (la lista de bloques) y `message.usage`. En las
grabaciones cada línea `assistant` tiene un solo bloque, y una respuesta con tres bloques se
convierte en tres líneas con el mismo `message.id`.

<!-- fixture: edit.stream.ndjson:15 -->
```json
{"type": "assistant", "message": {"id": "msg_011ChNjwF76zsXQUUbNF7yfr", "role": "assistant", "content": [{"type": "tool_use", "id": "toolu_015zZi1E7vQS17Zr1yMU1XbP", "name": "Read", "input": {"file_path": "C:\\demo\\app\\notes.md"}}]}, "parent_tool_use_id": null}
```

Los bloques que un lector conoce: `text`, `thinking`, `tool_use`, `tool_result`, `image` y los dos del advisor. Un
`message.model` igual a `<synthetic>` marca una línea escrita por el propio `claude`, no por el
modelo.

El advisor es una herramienta del servidor. La llamada es un bloque `server_tool_use` de nombre
`advisor`, y el veredicto, un bloque `advisor_tool_result`, viene en una línea `assistant`, no en
una `user`:

<!-- fixture: advisor.stream.ndjson:10 -->
```json
{"type": "assistant", "message": {"id": "msg_011ChJm4RCsC4MLQpjNGC4wg", "role": "assistant", "content": [{"type": "advisor_tool_result", "tool_use_id": "srvtoolu_01UsaBcWLXWZgtjtrmoWaNKX", "content": {"type": "advisor_redacted_result", "encrypted_content": "redacted"}}]}}
```

- El `content.type` del veredicto es `advisor_result` (con el `text`, y `stop_reason: "refusal"`
  cuando el advisor se niega), `advisor_redacted_result` (cifrado, sin texto que leer; el grabador
  cambia el `encrypted_content` por `redacted`) o `advisor_tool_result_error` (con el `error_code`).
  Solo el segundo está grabado.
- Un `server_tool_use` de otro nombre no da bloque, y un veredicto de `content.type` desconocido
  tampoco.

## Lo que vuelve al modelo: `user`

El resultado de una herramienta vuelve en una línea `user`, con un bloque `tool_result` en
`message.content` y el resultado estructurado de la herramienta en `tool_use_result`:

<!-- fixture: edit.stream.ndjson:19 -->
```json
{"type": "user", "message": {"role": "user", "content": [{"tool_use_id": "toolu_015zZi1E7vQS17Zr1yMU1XbP", "type": "tool_result", "content": "1\tline one\n…"}]}, "parent_tool_use_id": null, "tool_use_result": {"type": "text", "file": {"filePath": "C:\\demo\\app\\notes.md"}}, "tool_result_meta": [{"id": "toolu_015zZi1E7vQS17Zr1yMU1XbP", "permission_decision": {"decision": "accept", "source": "config", "reason_type": "mode"}}]}
```

- El `content` de un `tool_result` es un texto o una lista de bloques (texto e imagen). El lector
  junta los textos con `\n` y guarda las imágenes aparte.
- `is_error: true` marca un resultado de error. `tool_result_meta[].non_execution_kind` dice por qué
  la herramienta no se ejecutó, cuando no se ejecutó: `permission-rule` (denegada) o
  `user-rejected` (interrumpida).
- `claude` también escribe en líneas `user` textos que son suyos: el marcado de un comando de barra
  (`<local-command-stdout>`), el resumen después de una compactación, `[Request interrupted by user
  for tool use]`. El mensaje que manda el host no vuelve por el stdout como línea `user` (solo
  volvería con `--replay-user-messages`, que la biblioteca no pasa).

## El fin del turno: `result`

<!-- fixture: edit.stream.ndjson:75 -->
```json
{"type": "result", "subtype": "success", "is_error": false, "duration_ms": 3039, "duration_api_ms": 2777, "num_turns": 4, "total_cost_usd": 0.003943445, "usage": {"input_tokens": 6, "cache_creation_input_tokens": 25845, "cache_read_input_tokens": 50572, "output_tokens": 413}, "fast_mode_state": "off", "result": "Done. Line 3 of `notes.md` now reads `line 3 (edited)`, and `todo.md` was created with two bullet items: \"First item\" and \"Second item\"."}
```

- `subtype` es `success` o un error; en las grabaciones, el único error es
  `error_during_execution` (el turno interrumpido), con `is_error: true` y la lista `errors`.
- Los tokens de entrada que un lector suma: `input_tokens` + `cache_creation_input_tokens` +
  `cache_read_input_tokens`. Los de salida: `output_tokens`.
- El texto de un turno que falló: `result`, si lo hay; si no, el primer texto de `errors`.
- `duration_api_ms` se cuenta desde el comienzo del proceso, no del turno.

:::caution
El `result` de éxito puede esconder un rechazo anterior del turno
([#108132](https://github.com/anthropics/claude-code/issues/108132)). Quien necesita saber qué se
denegó lee las solicitudes de permiso y los `tool_result`, no solo el `result`.
:::

## El turno

Un turno va de un mensaje del usuario a su `result`:

1. el host escribe el mensaje en el stdin;
2. `claude` escribe `system/init` y `system/status` (`requesting`);
3. por cada respuesta del modelo: `stream_event` (`message_start`, los bloques, `message_stop`) y
   las líneas `assistant` de los bloques;
4. por cada herramienta: el `control_request` de `can_use_tool`, cuando necesita permiso, y la línea
   `user` con el resultado;
5. el `result`.

**Un mensaje enviado en medio del turno no abre otro.** En el escenario `queued`, se enviaron dos
mensajes, el segundo mientras el primero ejecutaba un comando; `claude` llevó el segundo al mismo
turno, y el stream tiene un solo `system/init` y un solo `result`. Contar mensajes enviados contra
`result` recibidos no cuadra. Ver "ocupada" en [Transporte](../transporte/#ocupada).

Las tareas en segundo plano (`system/task_*`) pueden terminar después del `result` del turno que
las abrió: en el escenario `subagent`, el `task_notification` llega en la línea 76, después del
`result` de la línea 72.

## El archivo de la sesión

Claude Code también graba cada sesión en disco, en un `.jsonl` (el "archivo de la sesión"). No es el
stdout: es otro formato, con otras líneas, y sirve para leer una conversación que ya ocurrió.

- **Los tipos:** `user`, `assistant`, `system` y `attachment`. En las 25 grabaciones
  (`fixtures/*.transcript.jsonl`) aparecen `user` (84), `assistant` (95), `system` con
  `compact_boundary` (2) e `informational` (1), y un `attachment` de `queued_command` (1).
- **Los nombres cambian.** El resultado estructurado de la herramienta es `toolUseResult`, no
  `tool_use_result`; el id de la sesión es `sessionId`; los metadatos de la compactación pueden
  venir como `compactMetadata`, con `preTokens` y `postTokens`.
- **No tiene los trozos.** Nada de `stream_event`, de `control_request` ni de `result`: solo los
  bloques enteros.
- **El mensaje del usuario está ahí**, como línea `user` con el texto, lo que no ocurre en el
  stdout. Un mensaje entregado en medio de un turno queda como `attachment` (`queued_command`).
- **El lector no tiene estado:** cada línea se lee sola.

<!-- fixture: edit.transcript.jsonl:1 -->
```json
{"type": "user", "uuid": "8b8a5b2f-b887-4d8f-baa5-fd637c77371a", "parentUuid": null, "isSidechain": false, "timestamp": "2026-11-25T00:59:51.422Z", "sessionId": "8f8ab1b3-6d82-4572-9c2c-6f5f166d7bd2", "message": {"role": "user", "content": "Use the Edit tool to change line 3 of notes.md to 'line 3 (edited)'. Then use the Write tool to create todo.md with two bullet items. Be brief."}}
```

Los eventos que salen de cada formato están en [Eventos](../eventos/), con la columna de origen.
