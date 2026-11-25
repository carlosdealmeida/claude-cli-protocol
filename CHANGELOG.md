# Changelog

All notable changes to this project are documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.1.0/).

## [Unreleased]

### Added
- **The specification on the site** (Portuguese first): six pages under `spec/`, for the protocol, the commands, the control requests, the transport, the events and the fixtures. Every example line is a line of a recording or of the stdin writer.
- `tools/test_site_spec.py`, which checks each example against the recording it names and the table of events against the expected results; CI runs it.

## [0.1.0] - 2026-11-24

The first release.

### Added
- Repository skeleton: documentation in five languages, CI, the site and an empty .NET solution for both targets.
- **The .NET library, `ClaudeCliProtocol`,** for `netstandard2.0` and `net6.0`, with no UI dependency. The events, the content blocks, `StreamJsonParser` and `TranscriptParser` come from Claude Visual Chat 0.10.0, where they lived inside the Avalonia control.
- **Commands as data.** `ClaudeCommand` and its records say what to send to the claude; `StdinWriter` writes the line. The answer to a permission request has no field for the tool's input: it comes from the request the claude sent.
- **`PermissionLedger`,** the permission requests that wait for an answer. Each one is handed out once, so a second answer to the same request finds nothing.
- **`IClaudeTransport` and `ReplayTransport`.** A transport carries lines one way and commands the other; `ReplayTransport` plays a recording.
- **`ClaudeSession`,** which reads a transport into typed events and keeps the state the lines tell: busy, pending permission requests, session id, confirmed mode and model. A read that throws leaves the session closed and idle; a command sent after the transport closed is not sent and answers false; an exception thrown by a `StateChanged` subscriber is dropped.
- **The 42 recordings of `fixtures/`** (21 scenarios) and the expected result of each one, in a neutral JSON format that a library in any language can produce.
- **`ProcessTransport`,** which starts `claude -p` on this machine and talks to it. The command line is a fixed list plus a few checked values (`ClaudeArguments`); the stderr is read and dropped; `ExitCode` tells how the claude ended; disposing ends its process tree (on .NET 6 or later, and on Windows). It answers a permission request from the request it read itself, once. A running claude holds no thread of the thread pool: the two reads have threads of their own. A start that fails, or a dispose during the start, leaves no process behind and a closed transport; a `StateChanged` subscriber that throws is dropped; a permission mode asked for at run time goes through the same list as the one of the start; the path of the claude, when given, must be absolute.
- **`ClaudeLocator`,** which finds `claude` on the PATH and never looks in the current folder.
- **A console example** in `examples/dotnet-console`, and an opt-in test against the real claude (`CCP_LIVE_CLAUDE=1`).
- **The fixture recorder** (`tools/gravar.py`), from Claude Visual Chat. The weekly workflow records the scenarios that have a recording, writes the expected results again and runs the privacy tests, in a job with no write access to the repository; a second job opens the pull request. The privacy tests also look for a credential and an e-mail address.
