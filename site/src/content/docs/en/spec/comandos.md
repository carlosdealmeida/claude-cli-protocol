---
title: Commands
description: What the host writes to the stdin of claude -p — the user message, with text, images and uuid — and how the line is encoded.
---

The host talks to `claude` by writing one JSON line per command to stdin. There are three shapes
of line:

| `type` | What it is | Where it is |
|---|---|---|
| `user` | A user message | On this page |
| `control_request` | A control request from the host to `claude`: `initialize`, interrupt, a change of mode or model… | [Control](../controle/#from-the-host-to-claude) |
| `control_response` | The host's answer to a request from `claude`, such as a permission request | [Control](../controle/#from-claude-to-the-host) |

The recordings in `fixtures/` keep only stdout. The lines on this page come from the `StdinWriter`
of the .NET library and from the recorder (`tools/gravar.py`), which sent the messages of the
recordings.

## The encoding

- **One line per command**, ended by `\n`, flushed after each one.
- **ASCII only.** Anything beyond ASCII is escaped as `\uXXXX` (`ã` becomes `\u00E3`). The line
  reaches `node` the same whatever encoding the system gives the pipe; on Windows, the pipe of a
  child process is not always UTF-8. This is a rule of the library, not of `claude`: it reads JSON,
  and an `ã` in UTF-8 is valid JSON too. The library chose not to depend on the encoding of the
  pipe.
- **Do not close stdin to end a turn.** With `--input-format stream-json`, the end of stdin after a
  turn ends the process at once and loses what was pending
  ([#111427](https://github.com/anthropics/claude-code/issues/111427)). Closing stdin is how the
  session is ended, and it is only done on purpose.

## The user message

<!-- stdin -->
```json
{"type":"user","message":{"role":"user","content":"Say hi in one line."},"parent_tool_use_id":null,"session_id":"default"}
```

- **`message.content`** is the text, when the message has no image.
- **`parent_tool_use_id`** is `null`: the message is for the main conversation.
- **`session_id`** is `"default"`. `claude` does not use it to choose the session: the session is
  the process's, a new one or the one of `--resume`. The real id arrives in the `system/init` line.

The same text shows up in the session file, as the first `user` line (see
[Protocol](../protocolo/#the-session-file)). On stdout it does not come back.

### With images

With an image, `content` becomes a list: first the image blocks, in order, then the text block. An
empty text yields no block.

<!-- stdin -->
```json
{"type":"user","message":{"role":"user","content":[{"type":"image","source":{"type":"base64","media_type":"image/png","data":"iVBORw0KGgo…"}},{"type":"text","text":"What two colors do you see? One line."}]},"parent_tool_use_id":null,"session_id":"default"}
```

The `data` is the base64 of the file, without the `data:` prefix. The `image` scenario of the
fixtures sends a 96 × 48 PNG image this way.

### With `uuid`

<!-- stdin -->
```json
{"type":"user","message":{"role":"user","content":"Say hi in one line."},"parent_tool_use_id":null,"session_id":"default","uuid":"5d0c9a3e-1b7f-4a52-9e64-0c2f8b1d7a41"}
```

With a `uuid`, `claude` tells what became of the message in `command_lifecycle` lines (since
2.1.310): `command_uuid` is the uuid sent, and `state` is `queued`, `started`, `completed`,
`cancelled`, `discarded` or `refused`. `ran_ahead: true` marks a command that `claude` ran at once,
while it was working. No recording has these lines; the event is
[`command_lifecycle`](../eventos/#the-ones-no-recording-exercises).

:::caution[Never resend with the `uuid` of another message]
2.1.327 fixed the loss of stored messages when a message was resent with the same uuid after the
process died. Each message has its own uuid; resending is sending a new message, with a new uuid.
:::

### With `inline_pastes`

<!-- stdin -->
```json
{"type":"user","message":{"role":"user","content":"…"},"parent_tool_use_id":null,"session_id":"default","inline_pastes":["…"]}
```

`inline_pastes` lists the long pasted texts that are still inside `content`. In the session file,
each long paste sits inside a wrapper, a line `<pasted_content id="abcd">` (four lowercase hex
digits), the pasted text and a line `</pasted_content id="abcd">`, and the library's reader takes
the wrapper off to show the message as it was written. No recording has a paste.

## Attached files

The protocol has no file block. The way the library uses is one line at the end of the text, one per
file, with the path on the `claude` machine:

```
Summarize this file.
[attached file: C:\demo\app\notes.md]
```

`claude` reads the file from there, with its own tools. Whoever reads the conversation back takes
those lines off the end of the message (only the ones at the end, and only those that are exactly
the marker) and shows the file apart. This is a convention of the library and of Claude Visual
Chat, not of `claude`.

## Slash commands

A slash command (`/compact`, `/context`, a command from a plugin) goes as the text of an ordinary
message:

<!-- stdin -->
```json
{"type":"user","message":{"role":"user","content":"/compact"},"parent_tool_use_id":null,"session_id":"default"}
```

The ones the session offers come in the reply to `initialize` (`commands`) and in a
`system/commands_changed` line when they change. The output of a local command comes back in a
`user` line with `<local-command-stdout>`; in the `commands` scenario, `/compact` gives back
`<local-command-stdout>Compacted </local-command-stdout>`.

:::note
Before 2.1.314, a message with `claude`'s own markup in its text (`<bash-stdout>`, for instance) was
not confirmed to a local stream-json host. What the library sees when it sends one like that with a
`uuid` has not been checked yet (item 6 of the [ROADMAP](https://github.com/carlosdealmeida/claude-cli-protocol/blob/master/docs/ROADMAP.md)).
:::
