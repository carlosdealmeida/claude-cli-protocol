# Changelog

All notable changes to this project are documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.1.0/).

## [Unreleased]

### Added
- Repository skeleton: documentation in five languages, CI, the site and an empty .NET solution for both targets.
- **The .NET library, `ClaudeCliProtocol`,** for `netstandard2.0` and `net6.0`, with no UI dependency. The events, the content blocks, `StreamJsonParser` and `TranscriptParser` come from Claude Visual Chat 0.10.0, where they lived inside the Avalonia control.
- **Commands as data.** `ClaudeCommand` and its records say what to send to the claude; `StdinWriter` writes the line. The answer to a permission request has no field for the tool's input: it comes from the request the claude sent.
- **`PermissionLedger`,** the permission requests that wait for an answer. Each one is handed out once, so a second answer to the same request finds nothing.
- **`IClaudeTransport` and `ReplayTransport`.** A transport carries lines one way and commands the other; `ReplayTransport` plays a recording.
- **`ClaudeSession`,** which reads a transport into typed events and keeps the state the lines tell: busy, pending permission requests, session id, confirmed mode and model.
- **The 42 recordings of `fixtures/`** (21 scenarios) and the expected result of each one, in a neutral JSON format that a library in any language can produce.
