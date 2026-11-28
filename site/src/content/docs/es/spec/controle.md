---
title: Control
description: Las solicitudes de control en los dos sentidos — del host a claude y de claude al host —, la solicitud de permiso, la respuesta y su regla.
---

Una solicitud de control es una línea `control_request` con un `request_id`, y la respuesta es una
línea `control_response` que repite ese id. Las solicitudes van en los dos sentidos:

- **del host a `claude`**: el `initialize`, el interrupt, el cambio de modo, de modelo y de effort,
  las consultas;
- **de `claude` al host**: la solicitud de permiso (`can_use_tool`) y las solicitudes de hook, de MCP
  y de elicitation, que este protocolo no atiende.

## La forma

La solicitud:

<!-- stdin -->
```json
{"type":"control_request","request_id":"mode-0","request":{"subtype":"set_permission_mode","mode":"acceptEdits"}}
```

La respuesta de éxito, con el cuerpo en `response.response` (cuando tiene cuerpo):

<!-- fixture: mode-and-model.stream.ndjson:2 -->
```json
{"type": "control_response", "response": {"subtype": "success", "request_id": "mode-0", "response": {"mode": "acceptEdits"}}}
```

La respuesta de error, con el texto en `response.error`:

<!-- stdin -->
```json
{"type":"control_response","response":{"subtype":"error","request_id":"…","error":"not supported"}}
```

- **El `request_id` es de quien pide.** El host elige el id de sus solicitudes; `claude` elige el de
  las suyas. La biblioteca genera un id nuevo por solicitud (un GUID de 32 dígitos, sin guiones):
  quien llama no lo elige. El grabador usa ids legibles (`init-1`, `mode-0`), y por eso aparecen en
  las grabaciones.
- **La respuesta no dice a qué solicitud responde, solo el id.** Quien pidió guarda la solicitud por
  el id para saber qué confirma la respuesta. En la biblioteca, `ClaudeSession` lo hace y entrega el
  par como `control_replied`.
- **El orden no está garantizado.** Una respuesta puede llegar en medio de las líneas de un turno.

## Del host a claude

| `subtype` | Qué pide | Cuerpo de la solicitud | Cuerpo de la respuesta | Grabado |
|---|---|---|---|---|
| `initialize` | El apretón de manos; los SDK oficiales lo mandan primero | — | `commands`, `models`, `account`, `auth_conflict` | todas |
| `interrupt` | Detiene el turno | — | `still_queued`: los mensajes que aún esperan su turno | `interrupted` |
| `set_permission_mode` | Cambia el modo | `mode` | `mode` | `mode-and-model`, `plan*` |
| `set_model` | Cambia el modelo | `model` | `model`, ya resuelto | `mode-and-model`, `effort` |
| `apply_flag_settings` | Cambia el effort | `settings.effortLevel` (texto o `null`) | ninguno | `effort` |
| `get_settings` | Los ajustes vigentes | — | `applied` (`model`, `effort`) | `effort` |
| `get_context_usage` | El uso de la ventana de contexto | — | `categories`, `totalTokens`, `maxTokens`, `percentage` | `commands` |
| `get_task_output` | Lo que una tarea en segundo plano imprimió | `task_id` | — | no |
| `stop_task` | Detiene una tarea en segundo plano | `task_id` | ninguno | `stop-task` |
| `remote_control` | Enciende o apaga Remote Control | `enabled` | `session_url`, al encender | no |
| `get_chrome_dialog` | El estado de la extensión del navegador | — | — | no |

### `initialize`

<!-- stdin -->
```json
{"type":"control_request","request_id":"init-1","request":{"subtype":"initialize"}}
```

La respuesta lista los comandos de barra de la sesión, los modelos y la cuenta:

<!-- fixture: edit.stream.ndjson:1 -->
```json
{"type": "control_response", "response": {"subtype": "success", "request_id": "init-1", "response": {"commands": [{"name": "compact", "description": "Free up context by summarizing the conversation so far", "argumentHint": "<optional custom summarization instructions>", "builtin": true}, "…"]}}}
```

- `commands[]`: `name` (sin la barra), `description`, `argumentHint`.
- `models[]`: `value`, `resolvedModel`, `displayName`, `description` y `supportedEffortLevels`.
- `account`: el login (`apiProvider`, `tokenSource`, `apiKeySource`), `email`, `organization` y
  `subscriptionType`. Las grabaciones no la tienen: el grabador la quita, porque es la cuenta de
  quien grabó.
- `auth_conflict`: cuando hay un token y una API key a la vez, los nombres de los dos orígenes
  (`token_source` y `api_key_source`), nunca las credenciales.

Todas las grabaciones empiezan por la respuesta del `initialize`, en la línea 1, porque el grabador
lo manda antes de cualquier mensaje. La línea `system/init` solo llega con el primer mensaje.

### `interrupt`

<!-- fixture: interrupted.stream.ndjson:22 -->
```json
{"type": "control_response", "response": {"subtype": "success", "request_id": "interrupt-1", "response": {"still_queued": []}}}
```

El turno interrumpido termina con un `result` de `error_during_execution`. Una solicitud de permiso
que estaba abierta se cancela antes (ver [`control_cancel_request`](#control_cancel_request)).

### `set_permission_mode` y `set_model`

El modo o el modelo valen a partir de la respuesta de éxito. La respuesta del `set_model` trae el
modelo resuelto: en el escenario `effort`, la solicitud de `haiku` vuelve como `claude-haiku-5-5`.

<!-- fixture: effort.stream.ndjson:14 -->
```json
{"type": "control_response", "response": {"subtype": "success", "request_id": "model-3", "response": {"model": "claude-haiku-5-5"}}}
```

Los modos que la biblioteca acepta pedir son `plan`, `default`, `acceptEdits` y `auto`. Un modo
fuera de esa lista no se escribe (ver [Transporte](../transporte/#la-comprobación)).

### `apply_flag_settings` y `get_settings`

<!-- stdin -->
```json
{"type":"control_request","request_id":"effort-1","request":{"subtype":"apply_flag_settings","settings":{"effortLevel":"high"}}}
```

La respuesta no confirma nada: es un éxito sin cuerpo. El effort vigente llega en la respuesta del
`get_settings`:

<!-- fixture: effort.stream.ndjson:2 -->
```json
{"type": "control_response", "response": {"subtype": "success", "request_id": "effort-1"}}
```

<!-- fixture: effort.stream.ndjson:3 -->
```json
{"type": "control_response", "response": {"subtype": "success", "request_id": "settings-1", "response": {"applied": {"model": "claude-haiku-5-5", "effort": "high"}}}}
```

`effortLevel: null` vuelve al effort por defecto del modelo. Desde la 2.1.325, un valor inválido se
rechaza con una respuesta de error.

### `get_context_usage`

<!-- fixture: commands.stream.ndjson:12 -->
```json
{"type": "control_response", "response": {"subtype": "success", "request_id": "context-2", "response": {"categories": [{"name": "System prompt", "tokens": 3129, "kind": "used"}, "…"], "totalTokens": 25341, "maxTokens": 1000000, "percentage": 3}}}
```

`kind` es `used`, `deferred`, `buffer` o `free`. Cuando vienen, `model`, `memoryFiles` y `agents`
completan la respuesta.

### `stop_task`

<!-- stdin -->
```json
{"type":"control_request","request_id":"stop-1","request":{"subtype":"stop_task","task_id":"bn9i1f619"}}
```

El `task_id` es el de la línea `system/background_tasks_changed` o `system/task_started`. El
`claude` termina la tarea y solo después responde. En el escenario `stop-task`, el
`task_notification` con `status: "stopped"` llega en la línea 30, la lista de tareas vacía en la
línea 31 y la respuesta en la línea 32:

<!-- fixture: stop-task.stream.ndjson:32 -->
```json
{"type": "control_response", "response": {"subtype": "success", "request_id": "stop-1", "response": {}}}
```

### Cómo se lee el cuerpo

La biblioteca no sabe a qué solicitud responde una respuesta: lee el cuerpo por su forma.

| Si el cuerpo tiene | Lee |
|---|---|
| `commands` | los comandos de barra; y, si tiene también `models` o `account`, el `auth_conflict` |
| `models` | los modelos |
| `applied` | los ajustes vigentes |
| `categories` y `totalTokens` | el uso del contexto |
| `account` | la cuenta |
| `rate_limits` | los límites de uso (la respuesta de un `get_usage`, que la biblioteca no pide pero lee) |
| `session_url` | el enlace de Remote Control |

## De claude al host

### `can_use_tool`, la solicitud de permiso

Con `--permission-prompt-tool stdio`, cada herramienta que necesita permiso se convierte en una
solicitud en el stdout, y `claude` espera la respuesta en el stdin:

<!-- fixture: edit.stream.ndjson:40 -->
```json
{"type": "control_request", "request_id": "0f8155c2-5135-4e0b-9da0-4cbc9d41ee1e", "request": {"subtype": "can_use_tool", "tool_name": "Edit", "display_name": "Edit", "input": {"file_path": "C:\\demo\\app\\notes.md", "old_string": "line three\n", "new_string": "line 3 (edited)\n", "replace_all": false}, "description": "notes.md", "permission_suggestions": [{"type": "setMode", "mode": "acceptEdits", "destination": "session"}], "tool_use_id": "toolu_01FPyYmYbFzFZR24ZJtHzcWB"}}
```

| Campo | Qué es |
|---|---|
| `tool_name` | La herramienta. Sin él, la biblioteca no genera evento, y el transporte responde con error |
| `display_name`, `description` | Cómo mostrar la solicitud |
| `input` | Lo que la herramienta va a hacer, tal como lo pidió el modelo |
| `permission_suggestions` | Las reglas que `claude` sugiere guardar para no volver a preguntar |
| `tool_use_id` | La llamada a una herramienta de la solicitud |
| `agent_id` | El subagente que pide (su `task_id`); ausente para la conversación principal |
| `decision_reason`, `decision_reason_type`, `decision_reason_code` | Por qué pregunta `claude`: el texto de un hook, de una comprobación de seguridad… El tipo es `hook`, `safetyCheck`, `subcommandResults`, `other`… |
| `default_to_no` | `claude` pide que el "no" venga seleccionado |
| `suppress_always_allow_rule` | `claude` no ofrece regla para guardar: sin "permitir siempre" |
| `mcp_server.source` | De dónde viene el servidor MCP de la herramienta; `claudeai` para un conector de claude.ai |

En las grabaciones aparecen solicitudes de `Edit`, `Write`, `Bash`, `PowerShell`, `ExitPlanMode` y
`AskUserQuestion`. La de un subagente tiene `agent_id` (`subagent.stream.ndjson`, línea 50); la de
un hook, `decision_reason_type: "hook"` (`hook-ask`, línea 20); la de una carpeta protegida,
`decision_reason_type: "safetyCheck"` (`seguranca`, línea 20).

### La respuesta de permiso

Tres decisiones, en una `control_response` de `success` con el `request_id` de la solicitud:

**Permitir**: el `input` vuelve tal como llegó, en `updatedInput`.

<!-- stdin -->
```json
{"type":"control_response","response":{"subtype":"success","request_id":"0f8155c2-5135-4e0b-9da0-4cbc9d41ee1e","response":{"behavior":"allow","updatedInput":{"file_path":"C:\\demo\\app\\notes.md","old_string":"line three\n","new_string":"line 3 (edited)\n","replace_all":false}}}}
```

**Permitir siempre**: lo mismo, más las `permission_suggestions` de la solicitud en
`updatedPermissions`, cuando son una lista.

<!-- stdin -->
```json
{"type":"control_response","response":{"subtype":"success","request_id":"…","response":{"behavior":"allow","updatedInput":{"…":"…"},"updatedPermissions":[{"type":"setMode","mode":"acceptEdits","destination":"session"}]}}}
```

**Denegar**: con el mensaje para el modelo. Sin mensaje, la biblioteca manda
`"The user denied this tool use."`.

<!-- stdin -->
```json
{"type":"control_response","response":{"subtype":"success","request_id":"…","response":{"behavior":"deny","message":"Do not create files, just explain."}}}
```

El mensaje llega al modelo como el texto de un `tool_result` con `is_error: true`. En el escenario
`denied`, el modelo lee el mensaje y explica en el `result` que no creó el archivo.

:::note[Salir del modo plan]
El fin de un plan es una solicitud de permiso de `ExitPlanMode`. Para salir del modo plan y seguir
con el plan, el grabador manda primero un `set_permission_mode` al modo siguiente (`acceptEdits` en
el escenario `plan`, `auto` en `plan-auto`) y después permite. Denegar con un mensaje devuelve el
plan al modelo con ese mensaje (escenario `plan-feedback`).
:::

### La regla de la respuesta de permiso

> La respuesta a una solicitud de permiso lleva siempre el `input` y las `permission_suggestions`
> que el propio lado del proceso recibió de `claude`. Quien llama solo elige el `request_id`, la
> decisión y el mensaje.

Es una regla de la biblioteca, no de `claude`: él acepta el `updatedInput` que reciba, y para eso
existe el campo. Pero quien tiene el stdin de un `claude` no debe ejecutar un `input` que vino de
otro lugar. Si la solicitud pasa por una interfaz gráfica, por una red o por otro proceso, quien
responde podría cambiar `"command": "ls"` por otro comando, y `claude` ejecutaría el comando
cambiado con el permiso dado al primero.

En la biblioteca:

- **el comando de respuesta (`AnswerPermission`) no tiene campo para el `input`** ni para las
  sugerencias: solo el id, la decisión y el mensaje;
- **quien escribe en el stdin arma la respuesta con la solicitud que él mismo leyó** del stdout,
  guardada en un libro de solicitudes (`PermissionLedger`);
- **cada solicitud se responde una vez.** Responder saca la solicitud del libro. También sale cuando
  `claude` la cancela, cuando llega el `tool_result` de su llamada y cuando el turno termina. Un id
  que no está pendiente (ya respondido, cancelado, resuelto o desconocido) no escribe nada.

Desde la 2.1.321, `AskUserQuestion` deniega la respuesta de un host que toca el `input` (campo
`afkTimeoutMs`). Con la regla, eso no ocurre.

:::caution[Aún no comprobado]
Con `--permission-prompt-tool`, un resultado grande de "allow" se graba en un archivo por el
tratamiento de salidas grandes de MCP ([#109249](https://github.com/anthropics/claude-code/issues/109249)).
La respuesta devuelve el `input` entero, así que un `Write` de un archivo grande pasa por ahí. Falta
verlo con un `claude` de verdad.
:::

### `control_cancel_request`

`claude` desiste de una solicitud de permiso que aún no tuvo respuesta:

<!-- fixture: interrupted.stream.ndjson:21 -->
```json
{"type": "control_cancel_request", "request_id": "dbfbeade-6899-46b2-ba43-120f8dea7acd"}
```

En el escenario `interrupted`, la solicitud de `PowerShell` de la línea 20 se cancela en la línea
21, por el interrupt. Después de la cancelación, la solicitud sale del libro, y una respuesta a ella
no escribe nada.

### La solicitud que nadie atiende

`claude` también puede mandar solicitudes de control de otros tipos: el callback de un hook del SDK,
un mensaje para un servidor MCP del SDK, una elicitation. Sin respuesta, espera para siempre. El
transporte de la biblioteca responde a cualquiera de ellas con error:

<!-- stdin -->
```json
{"type":"control_response","response":{"subtype":"error","request_id":"…","error":"not supported"}}
```

Y un `can_use_tool` que el lector no logra leer (sin `tool_name`, por ejemplo), y que por eso nunca
llegaría a quien responde, recibe el error `"malformed permission request"`.
