---
title: Comandos
description: Lo que el host escribe en el stdin de claude -p — el mensaje del usuario, con texto, imágenes y uuid — y cómo se codifica la línea.
---

El host habla con `claude` escribiendo una línea JSON por comando en el stdin. Hay tres formas de
línea:

| `type` | Qué es | Dónde está |
|---|---|---|
| `user` | Un mensaje del usuario | En esta página |
| `control_request` | Una solicitud de control del host a `claude`: el `initialize`, el interrupt, el cambio de modo o de modelo… | [Control](../controle/#del-host-a-claude) |
| `control_response` | La respuesta del host a una solicitud de `claude`, como la de permiso | [Control](../controle/#de-claude-al-host) |

Las grabaciones de `fixtures/` guardan solo el stdout. Las líneas de esta página salen del
`StdinWriter` de la biblioteca .NET y del grabador (`tools/gravar.py`), que mandó los mensajes de
las grabaciones.

## La codificación

- **Una línea por comando**, terminada en `\n`, con flush después de cada una.
- **Solo ASCII.** Lo que pasa de ASCII va escapado como `\uXXXX` (`ã` se convierte en `\u00E3`). La
  línea llega igual a `node` sea cual sea la codificación que el sistema da al pipe; en Windows, el
  pipe de un proceso hijo no siempre es UTF-8. Es una regla de la biblioteca, no de `claude`: él lee
  JSON, y un `ã` en UTF-8 también es JSON válido. La biblioteca eligió no depender de la
  codificación del pipe.
- **No cerrar el stdin para terminar un turno.** Con `--input-format stream-json`, el fin del stdin
  después de un turno termina el proceso en el acto y pierde lo que estaba pendiente
  ([#111427](https://github.com/anthropics/claude-code/issues/111427)). Cerrar el stdin es como se
  termina la sesión, y solo se hace a propósito.

## El mensaje del usuario

<!-- stdin -->
```json
{"type":"user","message":{"role":"user","content":"Say hi in one line."},"parent_tool_use_id":null,"session_id":"default"}
```

- **`message.content`** es el texto, cuando el mensaje no tiene imagen.
- **`parent_tool_use_id`** va `null`: el mensaje es para la conversación principal.
- **`session_id`** va `"default"`. `claude` no lo usa para elegir la sesión: la sesión es la del
  proceso, nueva o la del `--resume`. El id de verdad llega en la línea `system/init`.

El mismo texto aparece en el archivo de la sesión, como la primera línea `user` (ver
[Protocolo](../protocolo/#el-archivo-de-la-sesión)). Por el stdout no vuelve.

### Con imágenes

Con imagen, `content` se convierte en una lista: primero los bloques de imagen, en orden, y después
el bloque de texto. Un texto vacío no genera bloque.

<!-- stdin -->
```json
{"type":"user","message":{"role":"user","content":[{"type":"image","source":{"type":"base64","media_type":"image/png","data":"iVBORw0KGgo…"}},{"type":"text","text":"What two colors do you see? One line."}]},"parent_tool_use_id":null,"session_id":"default"}
```

El `data` es el base64 del archivo, sin el prefijo `data:`. El escenario `image` de las fixtures
manda así una imagen PNG de 96 × 48.

### Con `uuid`

<!-- stdin -->
```json
{"type":"user","message":{"role":"user","content":"Say hi in one line."},"parent_tool_use_id":null,"session_id":"default","uuid":"5d0c9a3e-1b7f-4a52-9e64-0c2f8b1d7a41"}
```

Con un `uuid`, `claude` cuenta qué fue del mensaje en líneas `command_lifecycle` (desde la
2.1.310): `command_uuid` es el uuid enviado, y `state` es `queued`, `started`, `completed`,
`cancelled`, `discarded` o `refused`. `ran_ahead: true` marca un comando que `claude` ejecutó en el
acto, mientras trabajaba. Ninguna grabación tiene estas líneas; el evento es
[`command_lifecycle`](../eventos/#los-que-ninguna-grabación-ejercita).

:::caution[Nunca reenviar con el `uuid` de otro mensaje]
La 2.1.327 corrigió la pérdida de mensajes guardados cuando un mensaje se reenviaba con el mismo
uuid después de que el proceso muriera. Cada mensaje tiene su uuid; reenviar es mandar un mensaje
nuevo, con uuid nuevo.
:::

### Con `inline_pastes`

<!-- stdin -->
```json
{"type":"user","message":{"role":"user","content":"…"},"parent_tool_use_id":null,"session_id":"default","inline_pastes":["…"]}
```

`inline_pastes` lista los textos largos pegados que siguen dentro de `content`. En el archivo de la
sesión, cada pegado largo queda dentro de un envoltorio, una línea `<pasted_content id="abcd">`
(cuatro dígitos hexadecimales en minúscula), el texto pegado y una línea
`</pasted_content id="abcd">`, y el lector de la biblioteca quita el envoltorio para mostrar el
mensaje tal como se escribió. Ninguna grabación tiene pegados.

## Los archivos adjuntos

El protocolo no tiene bloque de archivo. La forma que usa la biblioteca es una línea al final del
texto, una por archivo, con la ruta en la máquina de `claude`:

```
Resume este archivo.
[attached file: C:\demo\app\notes.md]
```

`claude` lee el archivo desde ahí, con sus herramientas. Quien lee la conversación de vuelta quita
esas líneas del final del mensaje (solo las del final, y solo las que son exactamente el marcado) y
muestra el archivo aparte. Es una convención de la biblioteca y de Claude Visual Chat, no de
`claude`.

## Los comandos de barra

Un comando de barra (`/compact`, `/context`, un comando de un plugin) va como texto de un mensaje
común:

<!-- stdin -->
```json
{"type":"user","message":{"role":"user","content":"/compact"},"parent_tool_use_id":null,"session_id":"default"}
```

Los que la sesión ofrece llegan en la respuesta del `initialize` (`commands`) y en una línea
`system/commands_changed` cuando cambian. La salida de un comando local vuelve en una línea `user`
con `<local-command-stdout>`; en el escenario `commands`, el `/compact` devuelve
`<local-command-stdout>Compacted </local-command-stdout>`.

:::note
Antes de la 2.1.314, un mensaje con el marcado del propio `claude` en el texto (`<bash-stdout>`, por
ejemplo) no se confirmaba a un host stream-json local. Lo que ve la biblioteca cuando manda uno así
con `uuid` aún no se comprobó (ítem 6 del [ROADMAP](https://github.com/carlosdealmeida/claude-cli-protocol/blob/master/docs/ROADMAP.md)).
:::
