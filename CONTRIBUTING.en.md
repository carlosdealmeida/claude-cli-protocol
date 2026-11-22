# Contributing

[Português](CONTRIBUTING.md) · **English** · [Español](CONTRIBUTING.es.md)

## Setup

There are three independent trees. Run from the root of the repository.

```bash
# The .NET library (SDK 8; the tests run on net6.0 and, on Windows, on net48)
dotnet build libs/dotnet -c Release
dotnet test libs/dotnet -c Release --no-build

# The site (Node 22.23.4 or newer)
cd site && npm ci && npm run build && npm run check && cd ..

# The shape of the repository: files, translations, links and workflows
python -I tools/test_repo.py -v
```

The structure test uses only Python's standard library. The tests that read YAML need PyYAML
(`python -m pip install pyyaml`) and are skipped without it.

## Project layout

```
site/          # landing and specification (Astro + Starlight), in five languages
docs/          # specs, plans, ROADMAP and glossary (docs/i18n)
fixtures/      # the recordings of claude -p and the expected result of each one
libs/dotnet/   # ClaudeCliProtocol (NuGet): netstandard2.0 and net6.0
libs/delphi/   # the Delphi library, floor 10.1 Berlin (phase 6)
examples/      # console examples
tools/         # the structure test and the package check
```

No UI goes in here. The Avalonia control lives in
[claude-visual-chat](https://github.com/carlosdealmeida/claude-visual-chat).

## How a change gets in

1. **Spec.** A decision about the protocol or the API becomes a file in [docs/specs/](docs/specs/).
2. **Plan.** The work becomes a plan in [docs/plans/](docs/plans/), in small tasks, each with its
   own test.
3. **One commit per task**, with the message in Portuguese: `feat(<area>): …`, `docs(<area>): …`,
   `test(<area>): …` or `chore: …`.
4. **TDD.** The test is written first and seen to fail; then comes the code that makes it pass.

A small fix (a typo, a link) needs neither a spec nor a plan. The specs and the plans are written
in Portuguese; an issue or a pull request in English is welcome.

## Fixtures

The recordings in `fixtures/` are real `claude -p` sessions. None goes in with personal data of
whoever recorded it: user name, folders, e-mail or credentials. The recorder replaces all of that
with demo values, and a test checks every file. The recorder and the test arrive with the library,
in phases 1 and 2.

## Translations

Portuguese is the source. A change to `README.md` applies to all five (`README.en.md`,
`README.es.md`, `README.zh-cn.md` and `README.zh-tw.md`), and the structure test complains when
one of them ends up with fewer sections. `SECURITY.md` and `CONTRIBUTING.md` have three versions:
Portuguese, English and Spanish.

The terms are in the glossary, at `docs/i18n/glossary.md`. The translations were produced by AI and
have not yet been reviewed by a native speaker: corrections are very welcome, and the glossary is
the best place to start.

## Code style

- Code, API names and comments in English.
- No comments by default. Add one only when the *why* is not obvious.
- The library depends on nothing beyond what the spec allows: `System.Text.Json` and, on
  `netstandard2.0`, `Microsoft.Bcl.AsyncInterfaces`.
- Delphi: the floor is 10.1 Berlin, so no inline variables and no type inference.

## Release

See [RELEASING.md](RELEASING.md) (in Portuguese).
