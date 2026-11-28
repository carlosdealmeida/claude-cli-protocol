---
title: Fixtures
description: Las grabaciones de sesiones reales de claude -p, el resultado esperado de cada una y cómo una biblioteca prueba que habla el protocolo.
---

Las fixtures son grabaciones de sesiones reales de `claude -p`, guardadas en
[`fixtures/`](https://github.com/carlosdealmeida/claude-cli-protocol/tree/master/fixtures). **La
conformidad es de ellas:** una biblioteca, en cualquier lenguaje, habla el protocolo cuando lee cada
grabación y produce el resultado esperado que está a su lado. La biblioteca .NET pasa las 50; la de
Delphi pasará las mismas.

## Los archivos

Cuatro archivos por escenario:

| Archivo | Qué es |
|---|---|
| `<escenario>.stream.ndjson` | El stdout de `claude -p`, una línea JSON por línea |
| `<escenario>.transcript.jsonl` | El archivo de la misma sesión, tal como Claude Code lo graba en disco |
| `<escenario>.stream.expected.json` | Los eventos que salen del `.stream.ndjson` |
| `<escenario>.transcript.expected.json` | Los eventos que salen del `.transcript.jsonl` |

Las grabaciones guardan solo lo que `claude` escribió. Lo que el grabador mandó por el stdin está en
el guion de cada escenario, en `tools/cenarios.json`.

## Los escenarios

| Escenario | Qué hace el guion | Qué muestra la grabación |
|---|---|---|
| `edit` | Pide un `Edit` y un `Write`; permite los dos | Dos solicitudes de permiso, los resultados de las herramientas |
| `bash` | Pide dos comandos de shell; permite | Solicitudes con `addRules` en las sugerencias, `decision_reason_type: subcommandResults` |
| `subagent` | Pide un subagente que lee tres archivos | `system/task_*`, una solicitud con `agent_id`, la tarea que termina después del `result` |
| `error` | Pide leer un archivo que no existe | Un `tool_result` de error |
| `denied` | Pide un `Write`; deniega con un mensaje | El mensaje como `tool_result` de error |
| `denied-long` | Lo mismo, con un mensaje de cuatro líneas | El mensaje largo, con los saltos de línea |
| `interrupted` | Pide un comando largo y manda `interrupt` sin responder a la solicitud | `control_cancel_request`, `still_queued`, `result` de `error_during_execution` |
| `mode-and-model` | Cambia el modo y el modelo, después pide un `Write`; permite siempre | Las respuestas de `set_permission_mode` y `set_model`, el "permitir siempre" |
| `image` | Manda una imagen PNG con una pregunta | Un `tool_result` con imagen (grabación de la 2.1.286) |
| `commands` | Pide el uso del contexto y ejecuta `/hello Ana` y `/compact` | La respuesta de `get_context_usage`, `<local-command-stdout>`, la compactación |
| `effort` | Cambia el effort y el modelo, y consulta los ajustes | `apply_flag_settings` sin cuerpo, `get_settings` con `applied` |
| `plan` | Pide un plan en modo plan; cambia a `acceptEdits` y permite el `ExitPlanMode` | La salida del modo plan |
| `plan-auto` | Lo mismo, cambiando a `auto` | La salida del modo plan al modo auto |
| `plan-feedback` | Pide un plan; deniega el `ExitPlanMode` con un mensaje | El plan que vuelve al modelo, un `AskUserQuestion` |
| `queued` | Manda un segundo mensaje mientras el primero ejecuta un comando | Dos mensajes, un turno: un `system/init` y un `result` |
| `queued-compact` | Manda `/compact` sin esperar y un mensaje después | `compact_boundary`, el `attachment` de `queued_command` en el archivo |
| `hook-blocked` | Manda un prompt que un hook `UserPromptSubmit` bloquea | `system/informational` de nivel `warning` |
| `hook-ask` | Un hook `PreToolUse` pide confirmación antes de un `Write` | `decision_reason_type: hook`, con el texto del hook |
| `error-long` | Ejecuta un comando que imprime 40 líneas en el stderr y sale con 3 | Un resultado de error largo, un `decision_reason` de comprobación |
| `error-wrapped` | Pide un `Edit` de un texto que no existe | El error de la herramienta |
| `seguranca` | Pide un `Write` en `.claude/settings.local.json`; deniega | `decision_reason_type: safetyCheck` |
| `bash-ansi` | Ejecuta un `printf` con colores de terminal, una señal sonora y un carácter de ancho cero | Un resultado de herramienta con códigos de terminal y caracteres invisibles |
| `advisor` | Pide una llamada al advisor, con `--advisor opus` | Los bloques `server_tool_use` y `advisor_tool_result`, con el veredicto cifrado |
| `workflow` | Pide un workflow de una fase, con un agente | `system/background_tasks_changed` con `local_workflow`, las líneas `system/task_*` del workflow |
| `stop-task` | Ejecuta `sleep 60` en segundo plano y manda `stop_task` dos segundos después | `task_notification` con `status: stopped`, la lista de tareas vacía, la respuesta del `stop_task` |

Son 25 escenarios grabados. El guion tiene un 26.º, `subagent-permission`, que nunca se grabó.
Veinte grabaciones son de Claude Code 2.1.331; la de `image` es de la 2.1.286, porque en la
regrabación del 2026-11-25 el modelo respondió sin abrir la imagen, y la grabación anterior es la
única con un resultado de herramienta que trae una imagen.
Las de `bash-ansi`, `advisor`, `workflow` y `stop-task` son de la 2.1.330: se grabaron en Claude Visual
Chat y vinieron de allí.

## El resultado esperado

Un JSON neutro, sin nada que sea de un solo lenguaje. La descripción completa está en el
[README de las fixtures](https://github.com/carlosdealmeida/claude-cli-protocol/blob/master/fixtures/README.md#o-formato-do-resultado-esperado-versão-1)
(en portugués); lo esencial:

<!-- fixture: edit.stream.expected.json:8 -->
```json
{"line": 2, "event": "session_started", "fields": {"session_id": "8f8ab1b3-6d82-4572-9c2c-6f5f166d7bd2", "model": "claude-haiku-5-5", "working_directory": "C:\\demo\\app", "permission_mode": "default", "claude_version": "2.1.331", "terminal_commands": [], "fast_mode_state": null, "ultrathink_available": null}}
```

- **`format`: 1**, y la lista `events`, un evento por línea del archivo.
- **`line`** es la línea de la grabación, contando desde 1 y sin contar las líneas en blanco. Una
  línea puede dar ninguno, uno o más eventos, y el orden de los eventos de una línea importa.
- **`event`** y las claves de **`fields`** son los nombres de [Eventos](../eventos/). El conjunto de
  claves de cada evento es fijo; un campo sin valor va `null`.
- **Enum** por el nombre en `snake_case` (`input_json`). **Duración** en milisegundos enteros.
  **Fecha** en ISO 8601, UTC, con exactamente tres decimales de milisegundo y `Z`, cortada y no
  redondeada.
- **Imagen:** el `data` se convierte en `{"length": <caracteres del base64>, "sha256": "<hex en
  minúscula de los bytes UTF-8 del texto base64>"}`.
- **La comparación es estructural.** El orden de las claves no importa; el de los ítems de una
  lista, sí. Dos números son iguales cuando difieren como mucho en `1e-9`.

## Cómo una biblioteca prueba la conformidad

1. Para cada `<escenario>.stream.ndjson`: un lector nuevo, **con estado entre las líneas** (el id del
   último `message_start` de cada `parent_tool_use_id`), lee el archivo línea a línea.
2. Para cada `<escenario>.transcript.jsonl`: un lector **sin estado** lee cada línea sola.
3. La biblioteca escribe los eventos en el formato de arriba y los compara, estructuralmente, con el
   `.expected.json`.
4. Una diferencia es un fallo, con la ruta hasta el valor diferente
   (`events[12].fields.block.input.file_path`, por ejemplo).

Las líneas se separan con `\n`; un `\n` al final se ignora, y una línea en blanco no cuenta.

### Qué prueba pasar

Los 50 resultados esperados los escribió la biblioteca .NET. Pasarlos prueba que una biblioteca lee
las grabaciones como las lee la biblioteca .NET, y que un cambio de comportamiento aparece como
diferencia. No prueba, por sí solo, que cada campo esté bien. Tres escenarios (`edit`, `denied` y
`mode-and-model`) se comprobaron desde fuera, con un script que lee la grabación cruda. Las
grabaciones ejercitan 27 de los 36 tipos de evento de los lectores; los otros nueve están en
[Eventos](../eventos/#los-que-ninguna-grabación-ejercita).

## La sanitización

Una grabación es una sesión real, y ninguna entra con datos personales de quien grabó. El grabador
(`tools/gravar.py`):

- cambia la carpeta, la home y el nombre de usuario por los de `demo` (`C:\demo\app`,
  `C:\Users\demo`), también cuando una ruta llega partida en varios deltas de un bloque;
- cambia por el nombre de la carpeta de `demo` (`C--demo-app`) la carpeta de proyecto que Claude
  Code crea bajo `Temp\claude`, cuyo nombre, desde la 2.1.330, son 16 dígitos hexadecimales sacados
  de la carpeta de quien grabó;
- cambia por `redacted` todo `encrypted_content`, como el del veredicto del advisor;
- deja en la línea `system/init` solo los campos conocidos, y quita las herramientas `mcp__`;
- deja en las respuestas del `initialize`, del `get_settings` y del `get_context_usage` solo los
  campos conocidos, y quita la cuenta;
- quita las líneas `rate_limit_event`, que son de la cuenta de quien grabó, y el
  `credential_expires_at` del `result`.

La prueba de privacidad (`FixturePrivacyTests`) busca en cada archivo una carpeta de otro usuario, una carpeta de proyecto con nombre de hash,
lo que parezca una credencial y una dirección de e-mail, y dice en qué línea y en qué campo lo
encontró, sin mostrar el valor. Ninguno de los dos entiende el texto de la conversación: quien graba
lee la grabación antes de mandarla.

## La regrabación semanal

Cada lunes, el workflow `fixtures.yml` vuelve a grabar, con el `claude` más nuevo, los escenarios que
ya tienen grabación, en dos jobs:

1. el que ejecuta el `claude` de verdad, sin permiso de escritura en el repositorio: graba, vuelve a
   escribir los resultados esperados y ejecuta las pruebas de conformidad y de privacidad sobre los
   archivos nuevos;
2. el que abre el pull request con las grabaciones y los esperados juntos, sin ejecutar `claude`.

Los ids de sesión y de mensaje cambian con cada grabación, así que hay un pull request por semana.
Como los esperados los escribe la propia biblioteca, quien frena una grabación mala es la persona que
lee el pull request: un evento que desaparece, un campo que cambia de forma. **Así un cambio del CLI
aparece aquí antes de aparecer en uso.** La primera regrabación que llegó hasta el final fue la del
2026-11-25.

## Grabar a mano

```bash
python -I tools/gravar.py edit        # un escenario
python -I tools/gravar.py --todos     # todos
python -I tools/test_gravar.py        # las pruebas del sanitizador (no arrancan claude)
```

El grabador arranca el `claude` del PATH (o el de `--claude <ruta>`) en una carpeta temporal, con la
cuenta de la máquina. Después de grabar, la biblioteca .NET vuelve a escribir los esperados:

```bash
CCP_UPDATE_EXPECTED=1 dotnet test libs/dotnet -c Release --framework net6.0 --filter "FullyQualifiedName~ConformanceTests"
```
