---
title: Fixtures
description: The recordings of real claude -p sessions, the expected result of each one and how a library proves it speaks the protocol.
---

The fixtures are recordings of real `claude -p` sessions, kept in
[`fixtures/`](https://github.com/carlosdealmeida/claude-cli-protocol/tree/master/fixtures).
**Conformance is theirs:** a library, in any language, speaks the protocol when it reads each
recording and produces the expected result next to it. The .NET library passes all 42; the Delphi
one will pass the same ones.

## The files

Four files per scenario:

| File | What it is |
|---|---|
| `<scenario>.stream.ndjson` | The stdout of `claude -p`, one JSON line per line |
| `<scenario>.transcript.jsonl` | The file of the same session, as Claude Code writes it to disk |
| `<scenario>.stream.expected.json` | The events that come out of the `.stream.ndjson` |
| `<scenario>.transcript.expected.json` | The events that come out of the `.transcript.jsonl` |

The recordings keep only what `claude` wrote. What the recorder sent on stdin is in the script of
each scenario, in `tools/cenarios.json`.

## The scenarios

| Scenario | What the script does | What the recording shows |
|---|---|---|
| `edit` | Asks for an `Edit` and a `Write`; allows both | Two permission requests, the tool results |
| `bash` | Asks for two shell commands; allows | Requests with `addRules` in the suggestions, `decision_reason_type: subcommandResults` |
| `subagent` | Asks for a sub-agent that reads three files | `system/task_*`, a request with `agent_id`, the task that ends after the `result` |
| `error` | Asks to read a file that does not exist | An error `tool_result` |
| `denied` | Asks for a `Write`; denies with a message | The message as an error `tool_result` |
| `denied-long` | The same, with a four-line message | The long message, with its line breaks |
| `interrupted` | Asks for a long command and sends `interrupt` without answering the request | `control_cancel_request`, `still_queued`, a `result` of `error_during_execution` |
| `mode-and-model` | Changes the mode and the model, then asks for a `Write`; allows always | The answers to `set_permission_mode` and `set_model`, the "allow always" |
| `image` | Sends a PNG image with a question | A `tool_result` with an image (recording from 2.1.286) |
| `commands` | Asks for the context usage and runs `/hello Ana` and `/compact` | The answer to `get_context_usage`, `<local-command-stdout>`, the compaction |
| `effort` | Changes the effort and the model, and queries the settings | `apply_flag_settings` with no body, `get_settings` with `applied` |
| `plan` | Asks for a plan in plan mode; switches to `acceptEdits` and allows `ExitPlanMode` | Leaving plan mode |
| `plan-auto` | The same, switching to `auto` | Leaving plan mode for auto mode |
| `plan-feedback` | Asks for a plan; denies `ExitPlanMode` with a message | The plan that goes back to the model, an `AskUserQuestion` |
| `queued` | Sends a second message while the first runs a command | Two messages, one turn: one `system/init` and one `result` |
| `queued-compact` | Sends `/compact` without waiting and a message after it | `compact_boundary`, the `attachment` of `queued_command` in the file |
| `hook-blocked` | Sends a prompt a `UserPromptSubmit` hook blocks | `system/informational` of level `warning` |
| `hook-ask` | A `PreToolUse` hook asks for confirmation before a `Write` | `decision_reason_type: hook`, with the text of the hook |
| `error-long` | Runs a command that prints 40 lines on stderr and exits with 3 | A long error result, a `decision_reason` from a check |
| `error-wrapped` | Asks for an `Edit` of a text that does not exist | The error of the tool |
| `seguranca` | Asks for a `Write` to `.claude/settings.local.json`; denies | `decision_reason_type: safetyCheck` |

There are 21 recorded scenarios. The script has a 22nd, `subagent-permission`, which was never
recorded. Twenty recordings are from Claude Code 2.1.331; the `image` one is from 2.1.286, because
in the re-recording of 2026-11-25 the model replied without opening the image, and the earlier
recording is the only one with a tool result that carries an image.

## The expected result

A neutral JSON, with nothing that belongs to a single language. The full description is in the
[README of the fixtures](https://github.com/carlosdealmeida/claude-cli-protocol/blob/master/fixtures/README.md#o-formato-do-resultado-esperado-versão-1)
(in Portuguese); the essentials:

<!-- fixture: edit.stream.expected.json:8 -->
```json
{"line": 2, "event": "session_started", "fields": {"session_id": "8f8ab1b3-6d82-4572-9c2c-6f5f166d7bd2", "model": "claude-haiku-5-5", "working_directory": "C:\\demo\\app", "permission_mode": "default", "claude_version": "2.1.331", "terminal_commands": [], "fast_mode_state": null, "ultrathink_available": null}}
```

- **`format`: 1**, and the `events` list, one event per line of the file.
- **`line`** is the line of the recording, counting from 1 and not counting blank lines. A line can
  give none, one or more events, and the order of the events of a line matters.
- **`event`** and the keys of **`fields`** are the names in [Events](../eventos/). The set of keys
  of each event is fixed; a field with no value is `null`.
- **Enum** by its name in `snake_case` (`input_json`). **Duration** in whole milliseconds.
  **Date** in ISO 8601, UTC, with exactly three millisecond digits and `Z`, cut and not rounded.
- **Image:** the `data` becomes `{"length": <characters of the base64>, "sha256": "<lowercase hex
  of the UTF-8 bytes of the base64 text>"}`.
- **The comparison is structural.** The order of the keys does not matter; the order of the items
  of a list does. Two numbers are equal when they differ by at most `1e-9`.

## How a library proves conformance

1. For each `<scenario>.stream.ndjson`: a new reader, **with state between lines** (the id of the
   last `message_start` of each `parent_tool_use_id`), reads the file line by line.
2. For each `<scenario>.transcript.jsonl`: a reader **with no state** reads each line on its own.
3. The library writes the events in the format above and compares them, structurally, with the
   `.expected.json`.
4. A difference is a failure, with the path to the value that differs
   (`events[12].fields.block.input.file_path`, for instance).

Lines are separated by `\n`; a `\n` at the end is ignored, and a blank line does not count.

### What passing proves

The 42 expected results were written by the .NET library. Passing them proves that a library reads
the recordings as the .NET library reads them, and that a change of behaviour shows up as a
difference. It does not prove, alone, that each field is right. Three scenarios (`edit`, `denied`
and `mode-and-model`) were checked from outside, with a script that reads the raw recording. The
recordings exercise 27 of the 36 kinds of event of the readers; the other nine are in
[Events](../eventos/#the-ones-no-recording-exercises).

## Sanitizing

A recording is a real session, and none goes in with personal data of whoever recorded it. The
recorder (`tools/gravar.py`):

- replaces the folder, the home and the user name with those of `demo` (`C:\demo\app`,
  `C:\Users\demo`), also when a path arrives split across several deltas of a block;
- keeps in the `system/init` line only the known fields, and drops the `mcp__` tools;
- keeps in the answers to `initialize`, `get_settings` and `get_context_usage` only the known
  fields, and drops the account;
- drops the `rate_limit_event` lines, which belong to the account of whoever recorded, and the
  `credential_expires_at` of the `result`.

The privacy test (`FixturePrivacyTests`) looks in each file for a folder of another user, anything
that looks like a credential and an e-mail address, and says on which line and in which field it
found it, without showing the value. Neither of the two understands the text of the conversation:
whoever records reads the recording before sending it.

## The weekly re-recording

Every Monday, the `fixtures.yml` workflow records again, with the newest `claude`, the scenarios
that already have a recording, in two jobs:

1. the one that runs the real `claude`, with no write permission on the repository: it records,
   writes the expected results again and runs the conformance and privacy tests on the new files;
2. the one that opens the pull request with the recordings and the expected results together,
   without running `claude`.

The session and message ids change with each recording, so there is one pull request a week. Since
the expected results are written by the library itself, what stops a bad recording is the person
who reads the pull request: an event that disappears, a field that changes shape. **That is how a
change in the CLI shows up here before it shows up in use.** The first re-recording that went all
the way through was that of 2026-11-25.

## Recording by hand

```bash
python -I tools/gravar.py edit        # one scenario
python -I tools/gravar.py --todos     # all of them
python -I tools/test_gravar.py        # the tests of the sanitizer (they do not start claude)
```

The recorder starts the `claude` on the PATH (or the one of `--claude <path>`) in a temporary
folder, with the machine's account. After recording, the expected results are written again by the
.NET library:

```bash
CCP_UPDATE_EXPECTED=1 dotnet test libs/dotnet -c Release --framework net6.0 --filter "FullyQualifiedName~ConformanceTests"
```
