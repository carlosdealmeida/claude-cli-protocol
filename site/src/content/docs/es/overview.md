---
title: Visión general
description: Qué es claude-cli-protocol, cómo se divide en capas y en qué fase está.
---

[Claude Code](https://github.com/anthropics/claude-code) habla una línea JSON por mensaje cuando
se ejecuta con `-p`. **claude-cli-protocol** reúne tres cosas que hoy están dispersas:

- la **especificación** escrita de ese protocolo;
- las **grabaciones** de sesiones reales que la prueban;
- las **bibliotecas** que lo hablan, primero en .NET y después en Delphi.

Proyecto de la comunidad, sin afiliación ni respaldo de Anthropic.

## Estado

**Versión 0.1.0, de 2026-11-24.** La biblioteca .NET está en NuGet:
`dotnet add package ClaudeCliProtocol`
([nuget.org](https://www.nuget.org/packages/ClaudeCliProtocol)). Lee y escribe el protocolo, inicia
el `claude -p` de esta máquina y pasa las 42 grabaciones del repositorio. El transporte se prueba
con un `claude` simulado; contra un `claude` real se ha ejecutado una vez, con un mensaje corto, en
una prueba que solo se ejecuta a petición.

## Las capas

```
claude-cli-protocol (biblioteca)  ← solo habla el protocolo; no conoce ninguna interfaz
   ↑
ClaudeVisualChat.Avalonia         ← solo dibuja; no abre procesos ni red
   ↑
tu aplicación                     ← integra
```

Cada capa solo conoce la de abajo, mediante un paquete con versión. En este repositorio no entra
ninguna interfaz gráfica.

## Cómo se llama a claude -p

```
claude -p --input-format stream-json --output-format stream-json --verbose
       --include-partial-messages --permission-prompt-tool stdio --permission-mode <modo>
```

`claude` escribe una línea JSON por evento en stdout y lee una línea JSON por comando en stdin.

## Fases

| Fase | Qué sale |
|---|---|
| 0 | El esqueleto: estructura, documentación, CI y sitio |
| 1 | La biblioteca .NET: eventos, parsers, comandos, sesión y reproducción de grabaciones |
| 2 | El transporte que inicia `claude`, el ejemplo de consola y la regrabación semanal de las fixtures |
| 5 | La especificación en este sitio: protocolo, comandos, control, transporte, fixtures |
| 6 | La biblioteca en Delphi, pasando las mismas fixtures |

Las fases 3, 4 y 7 ocurren en otros repositorios.
