# claude-cli-protocol

[Português](README.md) · [English](README.en.md) · **Español** · [简体中文](README.zh-cn.md) · [繁體中文](README.zh-tw.md)

[![CI](https://github.com/carlosdealmeida/claude-cli-protocol/actions/workflows/ci.yml/badge.svg)](https://github.com/carlosdealmeida/claude-cli-protocol/actions/workflows/ci.yml)
[![License: MIT](https://img.shields.io/badge/License-MIT-yellow.svg)](LICENSE)

**El protocolo stream-json de `claude -p`, especificado, grabado e implementado.**
[Claude Code](https://github.com/anthropics/claude-code) habla una línea JSON por mensaje cuando
se ejecuta con `-p`. Este repositorio reúne tres cosas que hoy están dispersas: la especificación
escrita de ese protocolo, las grabaciones de sesiones reales que la prueban y las bibliotecas que
lo hablan, primero en .NET y después en Delphi.

> Proyecto de la comunidad, sin afiliación ni respaldo de Anthropic.

## Estado

**Fase 1, a 2026-11-23.** La biblioteca .NET lee y escribe el protocolo: eventos, los dos parsers,
comandos, sesión y reproducción de grabaciones. El código vino de la carpeta `Protocol/` de [Claude
Visual Chat](https://github.com/carlosdealmeida/claude-visual-chat) y pasa las 42 grabaciones de
`fixtures/`, en los dos targets. Todavía no inicia `claude` por sí sola: ese transporte es la fase
2. No se ha publicado ningún paquete y el sitio todavía no está en línea.

## Qué hay aquí

| Carpeta | Qué es |
|---|---|
| `site/` | La landing y la especificación, en Astro + Starlight, en los cinco idiomas |
| `docs/` | Las specs, los planes, el ROADMAP y el glosario de las traducciones |
| `fixtures/` | Las grabaciones de `claude -p` y el resultado esperado de cada una |
| `libs/dotnet/` | `ClaudeCliProtocol`, para `netstandard2.0` y `net6.0` |
| `libs/delphi/` | La biblioteca en Delphi, con 10.1 Berlin como piso |
| `examples/` | Ejemplos de consola |
| `tools/` | La prueba de estructura del repositorio y la verificación del paquete |

## Las capas

```
claude-cli-protocol (biblioteca)  ← solo habla el protocolo; no conoce ninguna interfaz
   ↑
ClaudeVisualChat.Avalonia         ← solo dibuja; no abre procesos ni red
   ↑
tu aplicación                     ← integra
```

Cada capa solo conoce la de abajo, mediante un paquete con versión. En este repositorio no entra
ninguna interfaz gráfica, en ningún lenguaje.

## Cómo se llama a claude -p

```
claude -p --input-format stream-json --output-format stream-json --verbose
       --include-partial-messages --permission-prompt-tool stdio --permission-mode <modo>
```

Con estos argumentos, `claude` escribe una línea JSON por evento en stdout (el inicio de la
sesión, cada fragmento de la respuesta, cada llamada a una herramienta, cada solicitud de permiso,
el fin del turno) y lee una línea JSON por comando en stdin (el mensaje del usuario, la respuesta a
una solicitud de permiso, el cambio de modo o de modelo, la interrupción). La biblioteca convierte
las líneas en eventos tipados y los comandos en líneas.

## Seguridad

La respuesta a una solicitud de permiso lleva siempre el `input` y las `permission_suggestions` que
el lado del proceso recibió del propio `claude`. Quien envía el comando solo elige la solicitud, la
decisión y el mensaje: el comando de respuesta no tiene campo para el `input`. Quien escribe en el
stdin de `claude` arma la respuesta con la solicitud que él mismo leyó, y cada solicitud se responde
una sola vez.

Para reportar un fallo, consulta la [política de seguridad](SECURITY.es.md).

## Hoja de ruta

| Fase | Qué sale |
|---|---|
| 0 | El esqueleto: estructura, documentación, CI y sitio |
| 1 | La biblioteca .NET: eventos, parsers, comandos, sesión y reproducción de grabaciones |
| 2 | El transporte que inicia `claude`, el ejemplo de consola y la regrabación semanal de las fixtures |
| 5 | La especificación en el sitio: protocolo, comandos, control, transporte, fixtures |
| 6 | La biblioteca en Delphi, pasando las mismas fixtures |

Las fases 3, 4 y 7 ocurren en otros repositorios: Claude Visual Chat sobre esta biblioteca, la
aplicación que lo usa y el componente de chat VCL. El diseño completo está en
[docs/specs/](docs/specs/) (en portugués).

## Contribuir

Consulta [CONTRIBUTING.es.md](CONTRIBUTING.es.md).

## Licencia

[MIT](LICENSE)
