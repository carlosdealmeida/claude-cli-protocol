# claude-cli-protocol

[Português](README.md) · **English** · [Español](README.es.md) · [简体中文](README.zh-cn.md) · [繁體中文](README.zh-tw.md)

[![CI](https://github.com/carlosdealmeida/claude-cli-protocol/actions/workflows/ci.yml/badge.svg)](https://github.com/carlosdealmeida/claude-cli-protocol/actions/workflows/ci.yml)
[![License: MIT](https://img.shields.io/badge/License-MIT-yellow.svg)](LICENSE)

**The stream-json protocol of `claude -p`, specified, recorded and implemented.**
[Claude Code](https://github.com/anthropics/claude-code) speaks one JSON line per message when it
runs with `-p`. This repository brings together three things that are scattered today: the written
specification of that protocol, the recordings of real sessions that prove it, and the libraries
that speak it, first in .NET and then in Delphi.

> Community project, not affiliated with or endorsed by Anthropic.

## Status

**Phase 0, as of 2026-11-22.** The repository has its structure, the documentation in five
languages, CI and the skeleton of the site. The .NET library is still empty: the code arrives in
phase 1, from the `Protocol/` folder of
[Claude Visual Chat](https://github.com/carlosdealmeida/claude-visual-chat). No package has been
published and the site is not live yet.

## What is in here

| Folder | What it is |
|---|---|
| `site/` | The landing page and the specification, in Astro + Starlight, in five languages |
| `docs/` | The specs, the plans, the ROADMAP and the translation glossary |
| `fixtures/` | The recordings of `claude -p` and the expected result of each one |
| `libs/dotnet/` | `ClaudeCliProtocol`, for `netstandard2.0` and `net6.0` |
| `libs/delphi/` | The Delphi library, with 10.1 Berlin as its floor |
| `examples/` | Console examples |
| `tools/` | The structure test of the repository and the package check |

## The layers

```
claude-cli-protocol (library)  ← only speaks the protocol; knows no UI
   ↑
ClaudeVisualChat.Avalonia      ← only draws; opens no process and no network
   ↑
your application               ← integrates
```

Each layer knows only the one below it, through a versioned package. No UI goes into this
repository, in any language.

## How claude -p is called

```
claude -p --input-format stream-json --output-format stream-json --verbose
       --include-partial-messages --permission-prompt-tool stdio --permission-mode <mode>
```

With these arguments, `claude` writes one JSON line per event to stdout (the start of the session,
each piece of the reply, each tool call, each permission request, the end of the turn) and reads
one JSON line per command from stdin (the user's message, the answer to a permission request, a
mode or model change, the interrupt). The library turns the lines into typed events and the
commands into lines.

## Security

The answer to a permission request always carries the `input` and the `permission_suggestions` the
process side received from `claude` itself. The caller only chooses the request, the decision and
the message, and has no way to change what the tool will run.

To report a flaw, see the [security policy](SECURITY.en.md).

## Roadmap

| Phase | What comes out |
|---|---|
| 0 | The skeleton: structure, documentation, CI and site |
| 1 | The .NET library: events, parsers, commands, session and replay of recordings |
| 2 | The transport that starts `claude`, the console example and the weekly re-recording of the fixtures |
| 5 | The specification on the site: protocol, commands, control, transport, fixtures |
| 6 | The Delphi library, passing the same fixtures |

Phases 3, 4 and 7 happen in other repositories: Claude Visual Chat on top of this library, the
application that uses it, and the VCL chat component. The full design is in
[docs/specs/](docs/specs/) (in Portuguese).

## Contributing

See [CONTRIBUTING.en.md](CONTRIBUTING.en.md).

## License

[MIT](LICENSE)
