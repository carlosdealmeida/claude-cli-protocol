---
title: Transporte
description: Lo que lleva las líneas y los comandos — la interfaz, el proceso de claude, los argumentos y su comprobación, el arranque, el fin y los límites conocidos.
---

El transporte es lo que lleva las líneas de `claude` en un sentido y los comandos en el otro. Puede
ser el propio proceso, una red hasta un `claude` remoto o una grabación. Esta página dice qué hace un
transporte y, en detalle, qué hace el que arranca `claude` en la máquina (el `ProcessTransport` de la
biblioteca .NET). Lo que está aquí como regla es regla de la biblioteca; lo que es de `claude` está
dicho como tal.

## La interfaz

La interfaz es de comandos, no de stdin crudo:

```csharp
public interface IClaudeTransport : IAsyncDisposable
{
    /// The next stream-json line, or null once the transport has closed.
    Task<string?> ReceiveLineAsync(CancellationToken ct);

    /// A command; each transport delivers it in its own way.
    Task SendAsync(ClaudeCommand command, CancellationToken ct);

    /// Raised when the transport drops, comes back or closes.
    event EventHandler<TransportState>? StateChanged;
}
```

- **Líneas hacia arriba, comandos hacia abajo.** Quien usa el transporte recibe las líneas del
  stdout, una a una, y manda comandos: un mensaje, una respuesta de permiso, una solicitud de
  control. Quien escribe la línea del comando en el stdin es el lado del proceso.
- **Por qué comandos.** La respuesta de permiso se arma con la solicitud que leyó el lado del
  proceso (ver [la regla](../controle/#la-regla-de-la-respuesta-de-permiso)). Con stdin crudo, quien
  llama podría escribir cualquier `input`.
- **Los estados:** `Open` (las líneas vienen y los comandos van), `Dropped` (se cayó y puede volver:
  un transporte por red que se reconecta) y `Closed` (terminó: no viene ninguna línea más, y toda
  lectura devuelve nulo).
- **Los transportes de la biblioteca:** `ProcessTransport`, que arranca `claude`, y
  `ReplayTransport`, que reproduce una grabación e ignora los comandos. Un transporte por red es de
  la aplicación que lo usa, no de la biblioteca.

## El proceso

### Los argumentos

En este orden, y ninguno más:

```
-p --input-format stream-json --output-format stream-json --verbose
--include-partial-messages --permission-prompt-tool stdio --permission-mode <modo>
[--resume <id>] [--model <modelo>] [--effort <nível>]
```

Los tres últimos solo cuando se piden. **Nunca `--setting-sources`:** las reglas de bloqueo de cada
máquina valen en cualquier modo.

### La comprobación

En Windows, el `claude` instalado por npm es un `claude.cmd`, que pasa por `cmd.exe`. Ahí un carácter
suelto en un valor se convierte en otro comando. Por eso cada valor que va a la línea de comandos se
comprueba, y un valor rechazado no arranca nada:

| Valor | Aceptado |
|---|---|
| modo | `plan`, `default`, `acceptEdits` o `auto` |
| modelo | `^[A-Za-z0-9][A-Za-z0-9._:@/\[\]-]{0,99}$`: alias (`sonnet`, `opus[1m]`), ids, perfiles de Bedrock (`arn:…`) e ids de Vertex (`…@20250514`) |
| id de sesión | `^[A-Za-z0-9][A-Za-z0-9_-]{0,199}$` |
| effort | `low`, `medium`, `high`, `xhigh` o `max` |
| carpeta | no vacía |
| ruta de `claude` | absoluta, cuando se da |

Lo mismo vale para el modo pedido en uso: un `set_permission_mode` con un modo fuera de la lista no
se escribe. Ejemplos de valores que la comprobación frena: el modelo `sonnet & calc`, el id de
sesión `a" --dangerously-skip-permissions`, el modo `bypassPermissions`.

### Encontrar `claude`

Sin ruta dada, el transporte busca `claude` en el PATH, **solo en sus carpetas absolutas, nunca en la
carpeta actual**: la carpeta actual es la carpeta de la conversación, y un `claude.cmd` dejado ahí no
puede ser el que se ejecuta.

### El entorno

- El de esta máquina, más las variables que el host pasa para este `claude` (una variable con valor
  nulo se elimina). Entre ellas, `CLAUDE_CONFIG_DIR`, que elige la cuenta. El transporte no conoce
  ninguna variable por su nombre.
- Más **`NoDefaultCurrentDirectoryInExePath=1`**, para que `cmd.exe` no busque `node` en la carpeta
  de la conversación.
- La cuenta es la de la máquina: el login de la suscripción o la `ANTHROPIC_API_KEY`. Quien integra
  el chat en un producto de terceros debe usar API key.

### stdin, stdout y stderr

- **stdin:** una línea por comando, en bytes ASCII, con `\n` y flush (ver
  [Comandos](../comandos/#la-codificación)). Una escritura empezada va hasta el final: media línea
  rompería el stream. El transporte nunca cierra el stdin por su cuenta.
- **stdout:** UTF-8, leído línea a línea.
- **stderr:** leído y descartado. Leer es necesario, si no el pipe se llena y `claude` se cuelga;
  guardar no, porque puede tener texto de la conversación. La biblioteca solo cuenta las líneas.
- **Las dos lecturas van juntas, cada una en su hilo.** Leer el stdout hasta el final y solo después
  el stderr cuelga los dos lados. En la biblioteca, cada lectura tiene un hilo propio: con hilos del
  pool, cada `claude` vivo retenía tres de ellos durante toda su vida, y en una máquina de 4 núcleos
  dos transportes ya dejaban al resto del programa esperando.

## El arranque

1. Comprobar los valores y armar los argumentos.
2. Arrancar el proceso en la carpeta de la conversación.
3. **Empezar a leer el stdout y el stderr.**
4. **Escribir el `initialize`**, como hacen los SDK oficiales (se puede desactivar).

El orden de 3 y 4 importa. Hallado en pruebas: en la build `netstandard2.0`, bajo .NET Framework, la
escritura del `initialize` no terminaba cuando `claude` escribía mucho antes de leer el stdin;
termina cuando alguien lee el stdout. El motivo exacto no se averiguó.

- **Demora.** Desde la 2.1.323 hay informes de 5 a 7 segundos hasta el primer byte
  ([#109534](https://github.com/anthropics/claude-code/issues/109534)); la 2.1.325 pasó a esperar los
  datos de la cuenta antes de la primera solicitud, y la 2.1.327 espera hasta 10 segundos por los
  ajustes del modelo. Los plazos cortos de arranque fallan.
- **El arranque que falla** cierra el transporte: la lectura devuelve nulo, y el código de salida
  queda disponible. Desde la 2.1.314, un `claude -p` que no logra arrancar imprime el error en el
  stderr y sale con 1 (antes salía con 0 y sin salida). El código de salida es la única señal que
  queda, ya que el stderr se descarta.
- **Descartar durante el arranque** no deja proceso: el transporte comprueba de nuevo después de
  arrancar `claude` y, si ya fue descartado, termina el árbol.

## Ocupada

`ClaudeSession` dice si la sesión está ocupada. La regla sigue el stream, no la cuenta de mensajes:

> La sesión está ocupada desde un mensaje enviado, o desde un `system/init`, hasta el próximo
> `result`.

Contar turnos (cada mensaje abre uno, cada `result` cierra uno) no cuadra: en el escenario `queued`,
dos mensajes dieron un solo turno (ver [Protocolo](../protocolo/#el-turno)). El costo de la regla: un
mensaje que esperó su turno deja la sesión libre por un instante, entre el `result` del turno
anterior y su `system/init`.

La sesión también guarda el `session_id`, el modo y el modelo confirmados (por el `system/init`, por
el `system/status` y por las respuestas de éxito a las solicitudes) y las solicitudes de permiso
pendientes. Un comando mandado después de que el transporte se cierre no se envía, no deja la sesión
ocupada y devuelve `false`.

## El fin

- **El fin viene del proceso, no del fin del stdout.** Un hijo de `claude` que heredó el stdout
  puede mantenerlo abierto. Después de que el proceso termina, el transporte espera las últimas
  líneas (el `result`) durante un tiempo limitado (10 segundos en la biblioteca) y cierra.
- **El código de salida** queda disponible después del fin.
- **Terminar es matar el árbol de procesos, en el acto.** Sin intentar antes un fin educado: después
  de que `claude` sale por su cuenta, sus hijos ya no se pueden encontrar por su pid, y esperar
  dejaría procesos atrás. El costo: `claude` no termina una escritura en disco si el host lo
  descarta en medio de un turno.
- **Después del fin, mandar no hace nada:** ninguna escritura y ninguna excepción de pipe.

## Los límites conocidos

- En `netstandard2.0` fuera de Windows, terminar mata solo a `claude`, no a sus hijos.
- Un hijo que sobrevive a un `claude` que salió por su cuenta no se termina.
- `ClaudeLocator` no comprueba el bit de ejecución.
- Mientras un hijo huérfano mantiene el stdout, el hilo de lectura sigue vivo, pero ninguna línea se
  entrega después del fin.
- Contra un `claude` de verdad (2.1.331, en Windows), el transporte se ejecutó una vez, con un
  mensaje corto, en una prueba que solo corre a pedido (`CCP_LIVE_CLAUDE=1`). El resto de las
  pruebas usa un `claude` de mentira.

## Lo que el transporte nunca hace

- Grabar en disco la conversación, el stderr o credenciales.
- Pasar un argumento fuera de la lista, o un valor que no pasó por la comprobación.
- Escribir una respuesta de permiso con un `input` que no leyó del propio `claude`.
- Cerrar el stdin con un mensaje pendiente.
