# ClaudeCliProtocol

The stream-json protocol of `claude -p` ([Claude Code](https://github.com/anthropics/claude-code))
for .NET: typed events, commands, a session and swappable transports. No UI and no UI dependency.

> Community project, not affiliated with or endorsed by Anthropic.

## Status

**No release yet.** As of 2026-11-23 the library reads and writes the protocol and passes the 42
recordings of the repository, on both targets. It does not start `claude` by itself yet: the
transport that does arrives in phase 2. Follow the
[roadmap](https://github.com/carlosdealmeida/claude-cli-protocol/blob/master/README.en.md#roadmap).

## What is in it

- **Events:** `StreamJsonParser` turns the lines `claude -p --output-format stream-json` writes
  into `ClaudeEvent`s; `TranscriptParser` does the same for a session file (`.jsonl`). Neither
  throws: a line they do not understand gives no event.
- **Commands:** `ClaudeCommand` and its records (`SendUserMessage`, `AnswerPermission`,
  `SetPermissionMode`, `SetModel`, `Interrupt`…) are plain data. `StdinWriter` turns one into the
  JSON line for the stdin of `claude`.
- **Transport:** `IClaudeTransport` carries lines one way and commands the other.
  `ReplayTransport` plays a recording.
- **Session:** `ClaudeSession` reads a transport into events and keeps the state the lines tell:
  busy, pending permission requests, session id, confirmed mode and model.

Came from [Claude Visual Chat](https://github.com/carlosdealmeida/claude-visual-chat) 0.10.0, whose
`Protocol/` folder this library now holds on its own.

## Use

Read a line the claude wrote:

```csharp
using ClaudeCliProtocol;

var parser = new StreamJsonParser(); // one per conversation: it keeps state between lines
foreach (var e in parser.Parse(line))
{
    if (e is SessionStarted started) Console.WriteLine("session " + started.SessionId);
}
```

Play a recording through a session:

```csharp
await using var session = new ClaudeSession(ReplayTransport.FromFile(path));
await foreach (var e in session.ReadEventsAsync())
{
    if (e is PermissionRequested request)
    {
        // the answer names the request and the decision; the tool's input is not the caller's to send
        await session.SendAsync(new AnswerPermission(request.RequestId, PermissionDecision.Allow));
    }
}
```

Write a line for the claude. This is for the code that owns the stdin of `claude`:

```csharp
var message = StdinWriter.Write(new SendUserMessage("hello"));

// the ledger saw the request when it was read from the claude's stdout; it hands each request out once
if (ledger.TryTake(answer.RequestId, out var request))
{
    var line = StdinWriter.Write(answer, request!);
}
```

## The permission answer rule

The answer to a permission request always carries the tool's `input` and the
`permission_suggestions` the process side received from `claude` itself.

- **Who sends a command** chooses the request, the decision and the message. `AnswerPermission`
  has no field for the input or for the suggestions, and `ClaudeSession.SendAsync` sends an answer
  only for a request that is pending, once.
- **Who writes to the stdin of `claude`** builds the line. It keeps a `PermissionLedger` fed only
  with events it parsed itself from the claude's stdout, takes the request from it with `TryTake`
  (a second answer to the same request finds nothing) and gives that request to `StdinWriter`.
- **`StdinWriter.Write(answer, request)` trusts the request it is given.** It is a building block
  for the side that owns the process. That side must never pass it a request, an input or
  suggestions that came with a command or from whoever sent it; the library cannot enforce that for
  a caller that does otherwise.

The library has no transport that writes to a process yet (it arrives in phase 2), so today the
rule lives in the types and in the ledger, and no code path of the library writes an answer.

## Targets and dependencies

- `netstandard2.0` and `net6.0`.
- `System.Text.Json` 8.0.5 and, on `netstandard2.0` only, `Microsoft.Bcl.AsyncInterfaces` 8.0.0.
  Nothing else.

## Documentation

The repository has the documentation in five languages (Portuguese, English, Spanish, Simplified
and Traditional Chinese), the recordings the library is tested against, with the expected result
of each one, and the security policy:

<https://github.com/carlosdealmeida/claude-cli-protocol>

## License

[MIT](https://github.com/carlosdealmeida/claude-cli-protocol/blob/master/LICENSE)
