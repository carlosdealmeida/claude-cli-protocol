---
title: Overview
description: What claude-cli-protocol is, how it is layered and which phase it is in.
---

[Claude Code](https://github.com/anthropics/claude-code) speaks one JSON line per message when it
runs with `-p`. **claude-cli-protocol** brings together three things that are scattered today:

- the written **specification** of that protocol;
- the **recordings** of real sessions that prove it;
- the **libraries** that speak it, first in .NET and then in Delphi.

Community project, not affiliated with or endorsed by Anthropic.

## Status

**Phase 0, as of 2026-11-22.** The repository has its structure, the documentation, CI and this
site. The .NET library is still empty, and no package has been published.

## The layers

```
claude-cli-protocol (library)  ← only speaks the protocol; knows no UI
   ↑
ClaudeVisualChat.Avalonia      ← only draws; opens no process and no network
   ↑
your application               ← integrates
```

Each layer knows only the one below it, through a versioned package. No UI goes into this
repository.

## How claude -p is called

```
claude -p --input-format stream-json --output-format stream-json --verbose
       --include-partial-messages --permission-prompt-tool stdio --permission-mode <mode>
```

`claude` writes one JSON line per event to stdout and reads one JSON line per command from stdin.

## Phases

| Phase | What comes out |
|---|---|
| 0 | The skeleton: structure, documentation, CI and site |
| 1 | The .NET library: events, parsers, commands, session and replay of recordings |
| 2 | The transport that starts `claude`, the console example and the weekly re-recording of the fixtures |
| 5 | The specification on this site: protocol, commands, control, transport, fixtures |
| 6 | The Delphi library, passing the same fixtures |

Phases 3, 4 and 7 happen in other repositories.
