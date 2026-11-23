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

Write a line for the claude:

```csharp
var message = StdinWriter.Write(new SendUserMessage("hello"));
var answer = StdinWriter.Write(new AnswerPermission(request.RequestId, PermissionDecision.Deny, "not this file"), request);
```

## The permission answer rule

The answer to a permission request always carries the tool's `input` and the
`permission_suggestions` the process side received from `claude` itself. `AnswerPermission` has no
field for either: `StdinWriter` takes them from the `PermissionRequested` it is given, and
`PermissionLedger` keeps the requests that wait and hands each one out once, so a second answer to
the same request finds nothing. A caller chooses the request, the decision and the message, and
has no way to change what the tool will run.

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
