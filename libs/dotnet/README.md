# ClaudeCliProtocol

The stream-json protocol of `claude -p` ([Claude Code](https://github.com/anthropics/claude-code))
for .NET: typed events, commands, a session and swappable transports. No UI and no UI dependency.

> Community project, not affiliated with or endorsed by Anthropic.

## Status

**No release yet.** As of 2026-11-22 the project is an empty library that builds for both targets.
The code arrives in phase 1, from the `Protocol/` folder of
[Claude Visual Chat](https://github.com/carlosdealmeida/claude-visual-chat). Follow the
[roadmap](https://github.com/carlosdealmeida/claude-cli-protocol#roteiro).

## Targets and dependencies

- `netstandard2.0` and `net6.0`.
- `System.Text.Json` 8.0.5 and, on `netstandard2.0` only, `Microsoft.Bcl.AsyncInterfaces` 8.0.0.
  Nothing else.

## Documentation

The repository has the documentation in five languages (Portuguese, English, Spanish, Simplified
and Traditional Chinese), the recordings every library is tested against and the security policy:

<https://github.com/carlosdealmeida/claude-cli-protocol>

## License

[MIT](https://github.com/carlosdealmeida/claude-cli-protocol/blob/master/LICENSE)
