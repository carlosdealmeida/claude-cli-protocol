# Política de Seguridad

[Português](SECURITY.md) · [English](SECURITY.en.md) · **Español**

## Versiones soportadas

Las correcciones de seguridad se aplican solo a la última versión publicada en NuGet. Hasta la
primera publicación, valen para la rama `master`.

## Cómo reportar una vulnerabilidad

**No abras una issue pública** para fallos de seguridad.

Usa el reporte privado de vulnerabilidades de GitHub:

1. Ve a la pestaña **Security** del repositorio.
2. Haz clic en **Report a vulnerability**.
3. Describe el fallo, su impacto y, si es posible, los pasos para reproducirlo.

Enlace directo: <https://github.com/carlosdealmeida/claude-cli-protocol/security/advisories/new>

## Qué esperar

- **Confirmación de recepción:** en un plazo de 5 días hábiles.
- **Evaluación inicial:** en un plazo de 10 días hábiles, con una estimación de corrección.
- Se te mantiene informado del progreso hasta la resolución.
- Tras la corrección, publicamos un GitHub Security Advisory acreditando a quien reportó (salvo solicitud de anonimato).

## Alcance

Las bibliotecas de este repositorio inician `claude` en la máquina de quien las usa y hablan con
él por stdin y stdout. No se comunican con ningún servidor por cuenta propia. Se consideran
relevantes, entre otros:

- **El proceso que la biblioteca inicia.** El ejecutable, los argumentos y las variables de entorno
  que le pasa, entre ellas `CLAUDE_CONFIG_DIR`, que elige la cuenta. Un argumento que la biblioteca
  añada sin que se le pida, o una variable que se filtre a otro proceso, es un fallo.
- **Inyección en la línea de comandos.** En Windows, el `claude.cmd` de npm pasa por `cmd.exe`. Un
  modelo, un modo o un id de sesión que se convierta en otro comando o en otra opción es un fallo.
- **La regla de la respuesta de permiso.** La respuesta a una solicitud de permiso lleva siempre el
  `input` y las `permission_suggestions` que el lado del proceso recibió del propio `claude`; quien
  llama solo elige la solicitud, la decisión y el mensaje. Cualquier camino que permita a quien
  llama cambiar uno de los dos, o responder dos veces a la misma solicitud, es un fallo.
- **Lo que la biblioteca nunca escribe en disco.** La conversación, el stderr de `claude` (leído y
  descartado, porque puede contener texto de la conversación) y credenciales. Una escritura en
  disco que la biblioteca haga sin que se le pida es un fallo.
- **Datos personales en una grabación.** El nombre de usuario, una carpeta, el correo o una
  credencial de quien la grabó, dentro de un archivo de `fixtures/`.

La biblioteca .NET ya inicia el proceso y cumple la regla de la respuesta de permiso en
`ProcessTransport`. La biblioteca en Delphi aún no existe; el alcance también la cubre cuando
llegue.

Fuera de alcance: fallos del propio Claude Code (repórtalos a Anthropic) y vulnerabilidades en
dependencias de desarrollo que no forman parte del paquete publicado.
