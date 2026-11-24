# Security Policy

[Português](SECURITY.md) · **English** · [Español](SECURITY.es.md)

## Supported versions

Security fixes are applied only to the latest version published on NuGet. Until the first release,
they apply to the `master` branch.

## Reporting a vulnerability

**Do not open a public issue** for security flaws.

Use GitHub's private vulnerability reporting:

1. Go to the repository's **Security** tab.
2. Click **Report a vulnerability**.
3. Describe the flaw, its impact and, if possible, steps to reproduce.

Direct link: <https://github.com/carlosdealmeida/claude-cli-protocol/security/advisories/new>

## What to expect

- **Acknowledgement:** within 5 business days.
- **Initial assessment:** within 10 business days, with an estimated fix timeline.
- You are kept informed of progress until resolution.
- Once fixed, we publish a GitHub Security Advisory crediting the reporter (unless anonymity is requested).

## Scope

The libraries in this repository start `claude` on the machine of whoever uses them and talk to it
over stdin and stdout. They never talk to a server on their own. The following are considered in
scope, among others:

- **The process the library starts.** The executable, the arguments and the environment variables
  it passes, among them `CLAUDE_CONFIG_DIR`, which selects the account. An argument the library
  adds without being asked, or a variable that leaks to another process, is a flaw.
- **Command-line injection.** On Windows, the npm `claude.cmd` goes through `cmd.exe`. A model, a
  mode or a session id that turns into another command or another option is a flaw.
- **The permission response rule.** The answer to a permission request always carries the `input`
  and the `permission_suggestions` the process side received from `claude` itself; the caller only
  chooses the request, the decision and the message. Any path that lets the caller replace either
  one, or answer the same request twice, is a flaw.
- **What the library never writes to disk.** The conversation, the stderr of `claude` (read and
  discarded, because it may hold text of the conversation) and credentials. A write to disk the
  library makes without being asked is a flaw.
- **Personal data in a recording.** The user name, a folder, the e-mail or a credential of whoever
  recorded it, inside a file of `fixtures/`.

The .NET library already starts the process and keeps the permission answer rule in
`ProcessTransport`. The Delphi library does not exist yet; the scope covers it too when it arrives.

Out of scope: flaws in Claude Code itself (report them to Anthropic) and vulnerabilities in
development dependencies that are not part of the published package.
