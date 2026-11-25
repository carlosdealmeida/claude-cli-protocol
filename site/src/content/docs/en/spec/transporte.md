---
title: Transport
description: What carries the lines and the commands — the interface, the claude process, the arguments and their check, the start, the end and the known limits.
---

The transport is what carries the lines of `claude` one way and the commands the other. It can be
the process itself, a network to a remote `claude` or a recording. This page says what a transport
does and, in detail, what the one that starts `claude` on the machine does (the `ProcessTransport`
of the .NET library). What is here as a rule is a rule of the library; what belongs to `claude` is
said as such.

## The interface

The interface is of commands, not of raw stdin:

```csharp
public interface IClaudeTransport : IAsyncDisposable
{
    /// The next stream-json line, or null once the transport has closed.
    Task<string?> ReceiveLineAsync(CancellationToken ct);

    /// A command; each transport delivers it in its own way.
    Task SendAsync(ClaudeCommand command, CancellationToken ct);

    /// Raised when the transport drops, comes back or closes.
    event EventHandler<TransportState>? StateChanged;
}
```

- **Lines up, commands down.** Whoever uses the transport receives the stdout lines, one by one,
  and sends commands: a message, a permission answer, a control request. The one that writes the
  line of the command to stdin is the process side.
- **Why commands.** The permission answer is built from the request the process side read (see
  [the rule](../controle/#the-permission-answer-rule)). With raw stdin, the caller could write any
  `input`.
- **The states:** `Open` (lines come and commands go), `Dropped` (it fell and may come back: a
  network transport that reconnects) and `Closed` (it is over: no more lines come, and every read
  returns null).
- **The library's transports:** `ProcessTransport`, which starts `claude`, and `ReplayTransport`,
  which plays a recording and ignores the commands. A network transport belongs to the application
  that uses it, not to the library.

## The process

### The arguments

In this order, and no other:

```
-p --input-format stream-json --output-format stream-json --verbose
--include-partial-messages --permission-prompt-tool stdio --permission-mode <modo>
[--resume <id>] [--model <modelo>] [--effort <nível>]
```

The last three only when asked for. **Never `--setting-sources`:** the blocking rules of each
machine hold in any mode.

### The check

On Windows, the `claude` installed by npm is a `claude.cmd`, which goes through `cmd.exe`. There a
stray character in a value becomes another command. So every value that goes to the command line is
checked, and a refused value starts nothing:

| Value | Accepted |
|---|---|
| mode | `plan`, `default`, `acceptEdits` or `auto` |
| model | `^[A-Za-z0-9][A-Za-z0-9._:@/\[\]-]{0,99}$`: aliases (`sonnet`, `opus[1m]`), ids, Bedrock profiles (`arn:…`) and Vertex ids (`…@20250514`) |
| session id | `^[A-Za-z0-9][A-Za-z0-9_-]{0,199}$` |
| effort | `low`, `medium`, `high`, `xhigh` or `max` |
| folder | not empty |
| path of `claude` | absolute, when given |

The same holds for the mode asked for while running: a `set_permission_mode` with a mode outside the
list is not written. Examples of values the check stops: the model `sonnet & calc`, the session id
`a" --dangerously-skip-permissions`, the mode `bypassPermissions`.

### Finding `claude`

With no path given, the transport looks for `claude` on the PATH, **only in its absolute folders,
never in the current folder**: the current folder is the folder of the conversation, and a
`claude.cmd` left there cannot be the one that runs.

### The environment

- This machine's, plus the variables the host passes for this `claude` (a variable with a null
  value is removed). Among them, `CLAUDE_CONFIG_DIR`, which selects the account. The transport knows
  no variable by name.
- Plus **`NoDefaultCurrentDirectoryInExePath=1`**, so that `cmd.exe` does not look for `node` in
  the folder of the conversation.
- The account is the machine's: the subscription login or `ANTHROPIC_API_KEY`. Whoever embeds the
  chat in a third-party product should use an API key.

### stdin, stdout and stderr

- **stdin:** one line per command, in ASCII bytes, with `\n` and a flush (see
  [Commands](../comandos/#the-encoding)). A write that started goes to the end: half a line would
  break the stream. The transport never closes stdin on its own.
- **stdout:** UTF-8, read line by line.
- **stderr:** read and dropped. Reading is needed, otherwise the pipe fills and `claude` hangs;
  keeping is not, because it can hold text of the conversation. The library only counts the lines.
- **The two reads go together, each on its own thread.** Reading stdout to the end and only then
  stderr hangs both sides. In the library, each read has a thread of its own: on pool threads, each
  running `claude` held three of them for its whole life, and on a 4-core machine two transports
  already left the rest of the program waiting.

## The start

1. Check the values and build the arguments.
2. Start the process in the folder of the conversation.
3. **Start reading stdout and stderr.**
4. **Write `initialize`**, as the official SDKs do (it can be turned off).

The order of 3 and 4 matters. Found in testing: on the `netstandard2.0` build, under .NET Framework,
the write of `initialize` did not finish when `claude` wrote a lot before reading stdin; it finishes
once someone reads stdout. The exact reason was not found.

- **Delay.** Since 2.1.323 there are reports of 5 to 7 seconds to the first byte
  ([#109534](https://github.com/anthropics/claude-code/issues/109534)); 2.1.325 started waiting for
  the account data before the first request, and 2.1.327 waits up to 10 seconds for the model
  settings. Short start deadlines fail.
- **A start that fails** closes the transport: the read returns null, and the exit code is
  available. Since 2.1.314, a `claude -p` that cannot start prints the error on stderr and exits
  with 1 (before, it exited with 0 and no output). The exit code is the only signal left, since
  stderr is dropped.
- **Disposing during the start** leaves no process behind: the transport checks again after
  starting `claude` and, if it was already disposed, ends the tree.

## Busy

`ClaudeSession` says whether the session is busy. The rule follows the stream, not the count of
messages:

> The session is busy from a message sent, or from a `system/init`, until the next `result`.

Counting turns (each message opens one, each `result` closes one) does not add up: in the `queued`
scenario, two messages gave a single turn (see [Protocol](../protocolo/#the-turn)). The cost of the
rule: a message that waited its turn leaves the session free for a moment, between the `result` of
the previous turn and its own `system/init`.

The session also keeps the confirmed `session_id`, mode and model (from `system/init`, from
`system/status` and from the success answers to requests) and the pending permission requests. A
command sent after the transport closed is not sent, does not leave the session busy and returns
`false`.

## The end

- **The end comes from the process, not from the end of stdout.** A child of `claude` that inherited
  stdout can hold it open. After the process ends, the transport waits for the last lines (the
  `result`) for a limited time (10 seconds in the library) and closes.
- **The exit code** is available after the end.
- **Ending is killing the process tree, at once.** With no attempt at a polite end first: after
  `claude` exits on its own, its children can no longer be found by its pid, and waiting would leave
  processes behind. The cost: `claude` does not finish a write to disk if the host disposes it in
  the middle of a turn.
- **After the end, sending does nothing:** no write and no pipe exception.

## The known limits

- On `netstandard2.0` outside Windows, ending kills only `claude`, not its children.
- A child that outlives a `claude` that exited on its own is not ended.
- `ClaudeLocator` does not check the execute bit.
- While an orphan child holds stdout, the reading thread stays alive, but no line is handed out
  after the end.
- Against a real `claude` (2.1.331, on Windows), the transport ran once, with a short message, in a
  test that only runs on request (`CCP_LIVE_CLAUDE=1`). The rest of the tests use a fake `claude`.

## What the transport never does

- Write to disk the conversation, stderr or credentials.
- Pass an argument outside the list, or a value that did not go through the check.
- Write a permission answer with an `input` it did not read from `claude` itself.
- Close stdin with a message pending.
