---
title: Protocol
description: How claude -p is called, the shape of a line, the kinds of stdout line and the turn.
---

`claude -p` with `--input-format stream-json` and `--output-format stream-json` is a process that
reads one JSON line per command on stdin and writes one JSON line per event on stdout. This page
says what comes out on stdout. What goes into stdin is in [Commands](../comandos/) and
[Control](../controle/); the process, in [Transport](../transporte/).

:::caution[The protocol has no contract]
Anthropic does not document this protocol as a stable interface
([anthropics/claude-code#111046](https://github.com/anthropics/claude-code/issues/111046)). What is
here was checked against the recordings in `fixtures/`, made with Claude Code 2.1.331 (the `image`
scenario with 2.1.286), and against the code of the .NET library. A new version may change any
line. The weekly re-recording of the fixtures is what warns.
:::

## The call

```
claude -p --input-format stream-json --output-format stream-json --verbose
       --include-partial-messages --permission-prompt-tool stdio --permission-mode <modo>
```

- `--verbose` is required: without it, `claude -p` refuses `--output-format stream-json`.
- `--include-partial-messages` makes it write the pieces of the reply as it arrives (the
  `stream_event` lines).
- `--permission-prompt-tool stdio` makes it ask for permission on its own stdout, with a
  `control_request` of `can_use_tool`, and wait for the answer on stdin.

The optional ones (`--resume`, `--model`, `--effort`) and the check of each value are in
[Transport](../transporte/#the-arguments).

## The line

- **One line, one JSON object**, ended by `\n`. Stdout is UTF-8.
- **The `type` field** says what the line is. In `system`, the `subtype` says which notice; in
  `stream_event`, the `event.type`; in `control_request`, the `request.subtype`.
- **Almost every line carries `session_id` and `uuid`.** The `session_id` is the session's; the
  `uuid`, the line's.
- **A reader does not throw.** A blank line, a line that does not start with `{` (after stripping
  spaces and terminal codes from the start), invalid JSON, an unknown `type` or a missing field: the
  line yields no event, and reading goes on. That is what both readers of the .NET library do, and
  it is what keeps a library standing when `claude` gains a new line.
- **An unknown field is ignored.** The lines carry many more fields than a reader uses (the
  `result` has more than twenty). A reader reads the ones it knows.

## The kinds of line

Count in the 21 stdout recordings (`fixtures/*.stream.ndjson`):

| `type` | Subtype | In the recordings | What it is |
|---|---|---:|---|
| `system` | `init` | 28 | The start of a turn: session, model, folder, mode, version |
| `system` | `status` | 66 | What `claude` is doing (`requesting`, `compacting`, `null`) and the mode |
| `system` | `thinking_tokens` | 28 | How many tokens `claude` has thought so far in the block that is arriving |
| `system` | `task_started`, `task_progress`, `task_updated`, `task_notification` | 6 | A background task: a sub-agent or a command |
| `system` | `compact_boundary` | 2 | The conversation was compacted |
| `system` | `informational` | 1 | A notice from `claude` about its own work (a hook that blocked the prompt, for instance) |
| `system` | `background_tasks_changed` | 2 | The list of background tasks changed. The library does not read it |
| `stream_event` | `message_start` | 53 | A reply started to arrive |
| `stream_event` | `content_block_start`, `content_block_delta`, `content_block_stop` | 1,015 | The blocks of the reply, piece by piece |
| `stream_event` | `message_delta`, `message_stop` | 106 | The end of the reply. The library does not read them |
| `assistant` | | 86 | A whole block of the reply |
| `user` | | 45 | What goes back to the model: a tool result, a text from `claude` |
| `control_request` | `can_use_tool` | 19 | A permission request |
| `control_cancel_request` | | 1 | `claude` gave up on a permission request |
| `control_response` | `success` | 36 | The answer to a control request from the host |
| `result` | `success`, `error_during_execution` | 28 | The end of a turn |

There are lines the library reads that no recording has. In `system`: `api_retry`,
`hook_started`, `hook_response`, `commands_changed`, `bridge_state`, `local_command`,
`per_turn_effort_changed`, `model_refusal_fallback` and `peer_message_hold`. Outside `system`:
`rate_limit_event` (the recorder drops it, because it belongs to the account of whoever recorded)
and `command_lifecycle`. The events they yield are in [Events](../eventos/), marked as not
recorded.

## The start of the turn: `system/init`

A `system/init` line opens each turn, not each process. In the 21 recordings, the `system/init`
and the `result` lines alternate, one to one.

<!-- fixture: edit.stream.ndjson:2 -->
```json
{"type": "system", "subtype": "init", "session_id": "8f8ab1b3-6d82-4572-9c2c-6f5f166d7bd2", "model": "claude-haiku-5-5", "cwd": "C:\\demo\\app", "permissionMode": "default", "claude_code_version": "2.1.331", "tools": ["Task", "AskUserQuestion", "Bash", "…"]}
```

What a reader takes from it: `session_id`, `model`, `cwd`, `permissionMode` and
`claude_code_version`; when present, `terminal_slash_commands`, `fast_mode_state` and
`ultrathink_available`. The recordings keep only the known fields of the line: the recorder drops
the rest (see [Fixtures](../fixtures/#sanitizing)).

:::note
The mode in force is the one in this line, not the one asked for on the command line. Since 2.1.323,
`--permission-mode auto` starts in `default` when auto is not available
([#111343](https://github.com/anthropics/claude-code/issues/111343)).
:::

## The reply as it arrives: `stream_event`

With `--include-partial-messages`, each reply of the model first comes in pieces, in the streaming
format of the Anthropic API, inside `event`:

<!-- fixture: edit.stream.ndjson:5 -->
```json
{"type": "stream_event", "event": {"type": "content_block_start", "index": 0, "content_block": {"type": "tool_use", "id": "toolu_015zZi1E7vQS17Zr1yMU1XbP", "name": "Read", "input": {}}}, "parent_tool_use_id": null}
```

<!-- fixture: edit.stream.ndjson:7 -->
```json
{"type": "stream_event", "event": {"type": "content_block_delta", "index": 0, "delta": {"type": "input_json_delta", "partial_json": "{\"file_"}}, "parent_tool_use_id": null}
```

- **`message_start`** carries `event.message.id`, the id of the reply. The deltas that follow do
  not carry that id: **a reader keeps the id of the last `message_start` of each
  `parent_tool_use_id`** and puts it on the events of the blocks. That is why the stdout reader has
  state, and the session file reader does not.
- **`content_block_start`** opens block `index`. A `tool_use` starts with an empty `input` (`{}`).
- **`content_block_delta`** extends the block. The `delta.type` says which part: `text_delta`
  (field `text`), `thinking_delta` (field `thinking`, and `estimated_tokens` when present) or
  `input_json_delta` (field `partial_json`, a piece of the JSON of the `input`, which only closes at
  the end).
- **`content_block_stop`** closes the block.
- **`message_delta`** and **`message_stop`** close the reply. The library does not read them: the
  `assistant` line carries the whole block.
- **`parent_tool_use_id`** is the tool call of the sub-agent that writes the reply, or `null` for
  the main conversation.

## The whole block: `assistant`

After the pieces, `claude` writes the whole reply in `assistant` lines, in the message format of
the API: `message.id`, `message.content` (the list of blocks) and `message.usage`. In the
recordings each `assistant` line has a single block, and a reply with three blocks becomes three
lines with the same `message.id`.

<!-- fixture: edit.stream.ndjson:15 -->
```json
{"type": "assistant", "message": {"id": "msg_011ChNjwF76zsXQUUbNF7yfr", "role": "assistant", "content": [{"type": "tool_use", "id": "toolu_015zZi1E7vQS17Zr1yMU1XbP", "name": "Read", "input": {"file_path": "C:\\demo\\app\\notes.md"}}]}, "parent_tool_use_id": null}
```

The blocks a reader knows: `text`, `thinking`, `tool_use`, `tool_result` and `image`. A
`message.model` equal to `<synthetic>` marks a line written by `claude` itself, not by the model.

## What goes back to the model: `user`

A tool result comes back in a `user` line, with a `tool_result` block in `message.content` and the
structured result of the tool in `tool_use_result`:

<!-- fixture: edit.stream.ndjson:19 -->
```json
{"type": "user", "message": {"role": "user", "content": [{"tool_use_id": "toolu_015zZi1E7vQS17Zr1yMU1XbP", "type": "tool_result", "content": "1\tline one\n…"}]}, "parent_tool_use_id": null, "tool_use_result": {"type": "text", "file": {"filePath": "C:\\demo\\app\\notes.md"}}, "tool_result_meta": [{"id": "toolu_015zZi1E7vQS17Zr1yMU1XbP", "permission_decision": {"decision": "accept", "source": "config", "reason_type": "mode"}}]}
```

- The `content` of a `tool_result` is a text or a list of blocks (text and image). The reader joins
  the texts with `\n` and keeps the images apart.
- `is_error: true` marks an error result. `tool_result_meta[].non_execution_kind` says why the tool
  did not run, when it did not: `permission-rule` (denied) or `user-rejected` (interrupted).
- `claude` also writes, in `user` lines, texts of its own: the markup of a slash command
  (`<local-command-stdout>`), the summary after a compaction, `[Request interrupted by user for tool
  use]`. The message the host sends does not come back on stdout as a `user` line (it would only
  with `--replay-user-messages`, which the library does not pass).

## The end of the turn: `result`

<!-- fixture: edit.stream.ndjson:75 -->
```json
{"type": "result", "subtype": "success", "is_error": false, "duration_ms": 3039, "duration_api_ms": 2777, "num_turns": 4, "total_cost_usd": 0.003943445, "usage": {"input_tokens": 6, "cache_creation_input_tokens": 25845, "cache_read_input_tokens": 50572, "output_tokens": 413}, "fast_mode_state": "off", "result": "Done. Line 3 of `notes.md` now reads `line 3 (edited)`, and `todo.md` was created with two bullet items: \"First item\" and \"Second item\"."}
```

- `subtype` is `success` or an error; in the recordings, the only error is
  `error_during_execution` (the interrupted turn), with `is_error: true` and the `errors` list.
- The input tokens a reader adds up: `input_tokens` + `cache_creation_input_tokens` +
  `cache_read_input_tokens`. The output ones: `output_tokens`.
- The text of a turn that failed: `result`, if present; otherwise the first text of `errors`.
- `duration_api_ms` is counted from the start of the process, not of the turn.

:::caution
A successful `result` can hide an earlier refusal in the turn
([#108132](https://github.com/anthropics/claude-code/issues/108132)). Whoever needs to know what was
denied reads the permission requests and the `tool_result` blocks, not only the `result`.
:::

## The turn

A turn goes from a user message to its `result`:

1. the host writes the message to stdin;
2. `claude` writes `system/init` and `system/status` (`requesting`);
3. for each reply of the model: `stream_event` (`message_start`, the blocks, `message_stop`) and the
   `assistant` lines of the blocks;
4. for each tool: the `control_request` of `can_use_tool`, when it needs permission, and the `user`
   line with the result;
5. the `result`.

**A message sent in the middle of a turn does not open another one.** In the `queued` scenario,
two messages were sent, the second while the first was running a command; `claude` took the second
into the same turn, and the stream has a single `system/init` and a single `result`. Counting
messages sent against `result` lines received does not add up. See "busy" in
[Transport](../transporte/#busy).

Background tasks (`system/task_*`) can end after the `result` of the turn that opened them: in the
`subagent` scenario, the `task_notification` arrives on line 76, after the `result` on line 72.

## The session file

Claude Code also writes each session to disk, in a `.jsonl` (the "session file"). It is not stdout:
it is another format, with other lines, and it is for reading a conversation that already happened.

- **The kinds:** `user`, `assistant`, `system` and `attachment`. In the 21 recordings
  (`fixtures/*.transcript.jsonl`) there are `user` (76), `assistant` (84), `system` with
  `compact_boundary` (2) and `informational` (1), and one `attachment` of `queued_command` (1).
- **The names change.** The structured result of the tool is `toolUseResult`, not
  `tool_use_result`; the session id is `sessionId`; the compaction metadata may come as
  `compactMetadata`, with `preTokens` and `postTokens`.
- **It has no pieces.** No `stream_event`, no `control_request` and no `result`: only whole blocks.
- **The user message is there**, as a `user` line with the text, which does not happen on stdout. A
  message delivered in the middle of a turn stays as an `attachment` (`queued_command`).
- **The reader has no state:** each line is read on its own.

<!-- fixture: edit.transcript.jsonl:1 -->
```json
{"type": "user", "uuid": "8b8a5b2f-b887-4d8f-baa5-fd637c77371a", "parentUuid": null, "isSidechain": false, "timestamp": "2026-11-25T00:59:51.422Z", "sessionId": "8f8ab1b3-6d82-4572-9c2c-6f5f166d7bd2", "message": {"role": "user", "content": "Use the Edit tool to change line 3 of notes.md to 'line 3 (edited)'. Then use the Write tool to create todo.md with two bullet items. Be brief."}}
```

The events that come out of each format are in [Events](../eventos/), with the source column.
