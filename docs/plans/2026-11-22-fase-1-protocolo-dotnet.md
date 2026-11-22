# Fase 1: o protocolo em `libs/dotnet` — Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Trazer o `Protocol/` do Claude Visual Chat para a lib `ClaudeCliProtocol`, sem nenhum tipo de tela, e completar a lib com comandos como dados, a interface de transporte, o `ReplayTransport` e a `ClaudeSession`, tudo testado contra as gravações que já existem no CVC.

**Architecture:** O código de leitura vem copiado do CVC no commit `16809aa`, com três mudanças mecânicas: o namespace, dois nomes de tipo e as referências a tipos de tela. O que é novo fica em volta dele: os comandos (`ClaudeCommand`) e quem os escreve (`StdinWriter`), o livro de pedidos de permissão (`PermissionLedger`), que guarda o que o `claude` mandou e é o único caminho até a resposta, o transporte (`IClaudeTransport`, `ReplayTransport`) e a sessão (`ClaudeSession`), que lê linhas de um transporte, devolve eventos e acompanha o estado. Os arquivos ficam todos em `libs/dotnet/src/ClaudeCliProtocol/`, sem subpastas, com os nomes que têm no CVC, para a comparação com a origem ser direta.

**Tech Stack:** C# 10, `netstandard2.0;net6.0`, `System.Text.Json` 8.0.5, `Microsoft.Bcl.AsyncInterfaces` 8.0.0 (só no `netstandard2.0`), xunit 2.9.2.

**Spec:** [docs/specs/2026-11-22-claude-cli-protocol-design.md](../specs/2026-11-22-claude-cli-protocol-design.md)

## Global Constraints

- **Origem:** um clone do `claude-visual-chat` numa pasta fora deste repositório (`<refs>/claude-visual-chat`), no commit `16809aae4806fda21f9d317397b2b42f1cdbe0db`. Conferir com `git -C <refs>/claude-visual-chat rev-parse HEAD` antes de copiar qualquer arquivo. Se a pasta não existir ou o commit for outro, clonar e fazer `git fetch --depth 1 origin 16809aae4806fda21f9d317397b2b42f1cdbe0db && git checkout FETCH_HEAD`.
- **Caminhos da origem:** `SRC` = `src/ClaudeVisualChat.Avalonia`, `TESTS` = `tests/ClaudeVisualChat.Tests`, `FIX` = `tests/fixtures`.
- **Mudanças mecânicas em todo arquivo copiado**, e nenhuma outra:
  1. `namespace ClaudeVisualChat.Protocol` e `namespace ClaudeVisualChat` viram `namespace ClaudeCliProtocol`; nos testes, `ClaudeVisualChat.Tests` vira `ClaudeCliProtocol.Tests`, e os `using ClaudeVisualChat…` viram `using ClaudeCliProtocol;`.
  2. `ChatCommand` vira `SlashCommand`; `ChatModel` vira `ModelOption`.
  3. Um `<see cref>` para um tipo de tela (`ChatConversation`, `ChatFile`, `MessageSubmittedEventArgs`, `PendingPermission`…) vira texto sem `cref`, dizendo "the host" ou "the conversation". Com `TreatWarningsAsErrors`, um `cref` que não resolve é erro de build.
- **Qualquer outra diferença em relação à origem é uma decisão.** Se o código copiado não compilar ou um teste copiado não passar depois das três mudanças, parar e relatar; não adaptar a lógica.
- **Nada de tela:** nenhum `EventArgs`, nada de Avalonia, de SkiaSharp nem de `Markdown.Avalonia`. O teste `The_library_references_no_ui_package` da fase 0 continua valendo.
- **Público e interno:** ficam públicos os tipos que já eram públicos no CVC, mais `TaggedMessage`, `TaggedSection` e `UsageLimits.IsSnapshot`. `JsonHelpers`, `BlockReader`, `ContextBlock`, `ContextBlockReader`, `LightMarkup`, `MarkupElement`, `MarkupNode`, `LineText`, `CommandText`, `PastedContent`, `EventReader` e `TaskNotificationText` continuam internos.
- **A regra de segurança:** a resposta a um pedido de permissão leva sempre o `input` e as `permission_suggestions` que o lado do processo recebeu do `claude`. Nenhum tipo público deixa quem chama informar um dos dois.
- **Idioma:** código, nomes e comentários em inglês.
- **Testes:** `net6.0` e, no Windows, `net48`. Um teste que usa `record` ou `init` no projeto de teste precisa compilar nos dois.
- **Comandos**, da raiz do repositório:
  - `dotnet build libs/dotnet -c Release`
  - `dotnet test libs/dotnet -c Release --no-build`
  - um teste só: `dotnet test libs/dotnet -c Release --filter "FullyQualifiedName~<Classe>"`
- **Commits:** um por tarefa, mensagem em português, `feat(dotnet): …`, `test(dotnet): …`, `docs(…): …`. O commit de uma cópia diz o commit de origem.
- **Fora desta fase:** `ProcessTransport`, o exemplo de console, o gravador e a regravação (fase 2); a pausa do replay num pedido de permissão e o ritmo entre linhas, que o sample do CVC usa (fase 3); a publicação no NuGet.

## Review Focus

1. **Uma linha que não é um evento:** em branco, não JSON, com códigos de terminal na frente, de um tipo desconhecido, ou um JSON cortado no meio. Os parsers não levantam exceção e a sessão segue para a próxima linha. Teste `A_line_that_is_not_an_event_does_not_stop_the_session` (Tarefa 8).
2. **A segunda resposta ao mesmo pedido de permissão,** ou a resposta a um pedido que nunca existiu, foi cancelado ou já foi resolvido. Nada é enviado e `SendAsync` devolve `false`. Testes `A_request_is_taken_only_once` (Tarefa 6) e `An_answer_to_a_request_that_is_not_pending_sends_nothing` (Tarefa 8).
3. **A resposta de controle que chega antes de o envio voltar.** O supervisor de referência trata esse caso guardando o pedido antes de escrever. Teste `A_reply_that_arrives_before_the_send_returns_is_still_matched` (Tarefa 8).
4. **O transporte que fecha no meio de um turno.** A sessão termina a leitura sem exceção, deixa de estar ocupada e esvazia os pedidos pendentes. Teste `When_the_transport_closes_mid_turn_the_session_is_idle_with_nothing_pending` (Tarefa 8).
5. **Um `result` sem turno aberto** (um interrupt com a sessão parada também gera um). A contagem de turnos nunca fica negativa, e a mensagem seguinte ainda deixa a sessão ocupada. Teste `A_result_with_no_open_turn_does_not_go_negative` (Tarefa 8).

---

### Task 1: As gravações e o teste de privacidade

**Files:**
- Create: `fixtures/*.stream.ndjson` e `fixtures/*.transcript.jsonl` (42 arquivos, cópia de `FIX`)
- Create: `libs/dotnet/tests/ClaudeCliProtocol.Tests/FixtureFiles.cs` (cópia de `TESTS/FixtureFiles.cs`)
- Create: `libs/dotnet/tests/ClaudeCliProtocol.Tests/FixturePrivacyTests.cs` (cópia de `TESTS/FixturePrivacyTests.cs`)
- Modify: `libs/dotnet/tests/ClaudeCliProtocol.Tests/ClaudeCliProtocol.Tests.csproj`
- Modify: `fixtures/README.md`

**Interfaces:**
- Produces: `FixtureFiles.Scenarios` (os 21 nomes), `FixtureFiles.StreamLines(scenario)`, `FixtureFiles.TranscriptLines(scenario)` e `FixtureFiles.Directory`, como no CVC.

- [ ] **Step 1: Copiar os dois arquivos de teste e ligar a pasta de fixtures no `.csproj`**

No `.csproj`, como no CVC:

```xml
<None Include="..\..\..\..\fixtures\**\*" Exclude="..\..\..\..\fixtures\README.md"
      Link="fixtures\%(RecursiveDir)%(Filename)%(Extension)" CopyToOutputDirectory="PreserveNewest" />
<None Include="..\private-words.txt" Condition="Exists('..\private-words.txt')"
      Link="private-words.txt" CopyToOutputDirectory="PreserveNewest" />
```

Acrescentar a `FixturePrivacyTests` um teste que fixa o número de arquivos:

```csharp
[Fact]
public void There_are_two_recordings_for_each_of_the_21_scenarios()
{
    Assert.Equal(21, FixtureFiles.Scenarios.Length);
    foreach (var scenario in FixtureFiles.Scenarios)
    {
        Assert.NotEmpty(FixtureFiles.StreamLines(scenario));
        Assert.NotEmpty(FixtureFiles.TranscriptLines(scenario));
    }
}
```

- [ ] **Step 2: Rodar e ver falhar**

Run: `dotnet build libs/dotnet -c Release && dotnet test libs/dotnet -c Release --no-build --filter "FullyQualifiedName~FixturePrivacyTests"`
Expected: FAIL, a pasta `fixtures` ao lado do assembly de teste não tem gravações.

- [ ] **Step 3: Copiar as 42 gravações**

Copiar byte a byte de `FIX` para `fixtures/`. Conferir depois:

Run: `git add fixtures && git ls-files --eol fixtures | grep -v "i/lf"`
Expected: só o `README.md` pode aparecer; nenhuma gravação com CRLF no índice.

Run: `git ls-files fixtures | grep -c -E "\.(ndjson|jsonl)$"`
Expected: `42`.

- [ ] **Step 4: Rodar e ver passar**

Run: `dotnet build libs/dotnet -c Release && dotnet test libs/dotnet -c Release --no-build`
Expected: os testes de privacidade e o novo passam nos dois targets.

- [ ] **Step 5: Atualizar o `fixtures/README.md`**

A pasta deixou de estar vazia: os 21 cenários, os dois arquivos de cada um, a origem (gravadas pelo `tools/gravar.py` do `claude-visual-chat`, copiadas do commit `16809aa` em 2026-11-22) e que o resultado esperado chega na Tarefa 9.

- [ ] **Step 6: Commit**

```bash
git add -A
git commit -m "test(dotnet): gravações dos 21 cenários e teste de privacidade, do CVC 16809aa"
```

---

### Task 2: Os leitores de texto

**Files:**
- Create, em `libs/dotnet/src/ClaudeCliProtocol/`: `AnsiText.cs`, `AttachedFileLine.cs`, `LightMarkup.cs`, `TaggedMessage.cs`, `ContextBlocks.cs` (cópias de `SRC/Protocol/`)
- Create, em `libs/dotnet/tests/ClaudeCliProtocol.Tests/`: `AnsiTextTests.cs`, `TaggedMessageTests.cs`, `ContextBlockReaderTests.cs` (cópias de `TESTS/`)
- Create: `libs/dotnet/tests/ClaudeCliProtocol.Tests/PublicApiTests.cs`

**Interfaces:**
- Produces: `AnsiText`, `AttachedFileLine`, `TaggedMessage` e `TaggedSection` públicos, com os membros que têm no CVC. Os membros de `TaggedMessage` e de `TaggedSection` que eram `internal` só por causa do tipo passam a `public` junto com ele; os que usam um tipo interno (`MarkupNode`, por exemplo) continuam `internal`.
- Produces: `PublicApiTests`, com a lista de nomes públicos que as tarefas 3 a 8 aumentam.

- [ ] **Step 1: Copiar os três arquivos de teste e escrever o `PublicApiTests`**

`TaggedMessageTests.cs` cita dois tipos de fora do `Protocol/`. Se forem tipos de tela, o teste que os usa fica no CVC: tirar só esse teste e dizer qual no commit.

```csharp
private static readonly HashSet<string> Names =
    typeof(AnsiText).Assembly.GetExportedTypes().Select(t => t.FullName!).ToHashSet();

[Fact]
public void The_text_readers_are_public()
{
    foreach (var name in new[] { "AnsiText", "AttachedFileLine", "TaggedMessage", "TaggedSection" })
        Assert.Contains("ClaudeCliProtocol." + name, Names);
}

[Fact]
public void Helpers_stay_internal()
{
    foreach (var name in new[] { "LightMarkup", "MarkupElement", "MarkupNode", "ContextBlock", "ContextBlockReader" })
        Assert.DoesNotContain("ClaudeCliProtocol." + name, Names);
}

[Fact]
public void Every_public_type_is_in_the_one_namespace()
{
    Assert.All(typeof(AnsiText).Assembly.GetExportedTypes(), t => Assert.Equal("ClaudeCliProtocol", t.Namespace));
}
```

- [ ] **Step 2: Rodar e ver falhar**

Run: `dotnet build libs/dotnet -c Release`
Expected: FAIL de compilação, `AnsiText` não existe.

- [ ] **Step 3: Copiar os cinco arquivos e aplicar as mudanças mecânicas**

- [ ] **Step 4: Rodar e ver passar**

Run: `dotnet build libs/dotnet -c Release && dotnet test libs/dotnet -c Release --no-build`
Expected: build sem avisos; os testes copiados passam nos dois targets.

- [ ] **Step 5: Commit**

```bash
git add -A
git commit -m "feat(dotnet): leitores de texto (AnsiText, AttachedFileLine, TaggedMessage, ContextBlocks), do CVC 16809aa"
```

---

### Task 3: O catálogo

**Files:**
- Create: `libs/dotnet/src/ClaudeCliProtocol/Catalog.cs` (cópia de `SRC/ChatCatalog.cs`)
- Create: `libs/dotnet/tests/ClaudeCliProtocol.Tests/CatalogTests.cs`
- Modify: `libs/dotnet/tests/ClaudeCliProtocol.Tests/PublicApiTests.cs`

**Interfaces:**
- Produces: os records `SlashCommand(Name, Description, ArgumentHint)`, `ModelOption(Value, ResolvedModel, DisplayName, Description, EffortLevels)`, `ContextCategory`, `ContextFile`, `ContextAgent`, `ContextUsage`, `UsageWindow`, `UsageLimits` (com `public bool IsSnapshot { get; init; }`), `AccountInfo` e `AuthConflict`, com os membros do CVC.

- [ ] **Step 1: Escrever os testes que falham**

```csharp
[Fact]
public void The_renamed_types_keep_their_shape()
{
    var command = new SlashCommand("review", "Review a pull request", "<pr>");
    Assert.Equal("review", command.Name);
    var model = new ModelOption("opus", "claude-opus-5-5", "Opus 5.5", "For complex work", new[] { "low", "high" });
    Assert.Equal(new[] { "low", "high" }, model.EffortLevels);
}

[Fact]
public void A_snapshot_of_the_limits_says_so()
{
    Assert.False(new UsageLimits(Array.Empty<UsageWindow>()).IsSnapshot);
    Assert.True(new UsageLimits(Array.Empty<UsageWindow>()) { IsSnapshot = true }.IsSnapshot);
}
```

Em `PublicApiTests`:

```csharp
[Fact]
public void The_catalog_uses_the_names_of_this_library()
{
    Assert.Contains("ClaudeCliProtocol.SlashCommand", Names);
    Assert.Contains("ClaudeCliProtocol.ModelOption", Names);
    Assert.DoesNotContain("ClaudeCliProtocol.ChatCommand", Names);
    Assert.DoesNotContain("ClaudeCliProtocol.ChatModel", Names);
    Assert.True(typeof(UsageLimits).GetProperty("IsSnapshot")!.GetMethod!.IsPublic);
}
```

- [ ] **Step 2: Rodar e ver falhar**

Run: `dotnet build libs/dotnet -c Release`
Expected: FAIL de compilação, `SlashCommand` não existe.

- [ ] **Step 3: Copiar `ChatCatalog.cs` para `Catalog.cs`**, com as mudanças mecânicas e `IsSnapshot` público.

- [ ] **Step 4: Rodar e ver passar**

Run: `dotnet build libs/dotnet -c Release && dotnet test libs/dotnet -c Release --no-build`
Expected: tudo verde nos dois targets.

- [ ] **Step 5: Commit**

```bash
git add -A
git commit -m "feat(dotnet): catálogo da sessão (SlashCommand, ModelOption, uso e conta), do CVC 16809aa"
```

---

### Task 4: Os eventos e os dois parsers

**Files:**
- Create, em `libs/dotnet/src/ClaudeCliProtocol/`: `ContentBlocks.cs`, `ClaudeEvents.cs`, `StreamJsonParser.cs`, `TranscriptParser.cs` (cópias de `SRC/Protocol/`)
- Create, em `libs/dotnet/tests/ClaudeCliProtocol.Tests/`: `StreamJsonParserTests.cs`, `TranscriptParserTests.cs` (cópias de `TESTS/Protocol/`)
- Modify: `libs/dotnet/tests/ClaudeCliProtocol.Tests/PublicApiTests.cs`

**Interfaces:**
- Produces: `ClaudeEvent` e os 36 records que o estendem, `ContentBlock` e os cinco blocos, `DeltaKind`, `MessageOrigin`, `StreamJsonParser.Parse(string line) -> IReadOnlyList<ClaudeEvent>` (de instância) e `TranscriptParser.Parse(string line) -> IReadOnlyList<ClaudeEvent>` (estático). Os que as tarefas seguintes usam:
  - `SessionStarted(SessionId, Model, WorkingDirectory, PermissionMode, ClaudeVersion)`
  - `StatusChanged(Status, PermissionMode)`
  - `PermissionRequested(RequestId, ToolName, DisplayName, Description, ToolUseId, Input, Suggestions)`
  - `PermissionCancelled(RequestId)`
  - `ControlAcknowledged(RequestId, IsError, Response)`, com `Error`
  - `TurnCompleted(Subtype, IsError, DurationMs, CostUsd, InputTokens, OutputTokens)`
  - `UserBlock(ParentToolUseId, Block, ToolUseResult, IsMeta, IsCompactSummary)` e `ToolResultBlock(ToolUseId, Text, IsError)`

- [ ] **Step 1: Copiar os dois arquivos de teste**

`StreamJsonParserTests.cs` cita um tipo de fora do `Protocol/`: tratar como na Tarefa 2. Acrescentar a `PublicApiTests`:

```csharp
[Fact]
public void The_readers_and_the_events_are_public()
{
    foreach (var name in new[] { "StreamJsonParser", "TranscriptParser", "ClaudeEvent", "ContentBlock",
                                 "TextContentBlock", "PermissionRequested", "TurnCompleted" })
        Assert.Contains("ClaudeCliProtocol." + name, Names);
    Assert.DoesNotContain("ClaudeCliProtocol.TextBlock", Names); // the name the CVC avoided, kept avoided
    Assert.Equal(36, typeof(ClaudeEvent).Assembly.GetExportedTypes().Count(t => t.IsSubclassOf(typeof(ClaudeEvent))));
}
```

- [ ] **Step 2: Rodar e ver falhar**

Run: `dotnet build libs/dotnet -c Release`
Expected: FAIL de compilação, `StreamJsonParser` não existe.

- [ ] **Step 3: Copiar os quatro arquivos e aplicar as mudanças mecânicas**

- [ ] **Step 4: Rodar e ver passar**

Run: `dotnet build libs/dotnet -c Release && dotnet test libs/dotnet -c Release --no-build`
Expected: build sem avisos; todos os testes copiados passam nos dois targets. O número de 36 eventos vale até a Tarefa 8, que acrescenta um.

- [ ] **Step 5: Conferir que a cópia é só mecânica**

Run, para cada um dos dez arquivos de `SRC/Protocol/` que vieram (todos menos `StdinMessages.cs`):
`git diff --no-index --stat <refs>/claude-visual-chat/src/ClaudeVisualChat.Avalonia/Protocol/<arquivo> libs/dotnet/src/ClaudeCliProtocol/<arquivo>`

Ler o diff de cada um. Expected: só linhas de `namespace`, os dois nomes trocados, `cref` de tela e os `public` de `TaggedMessage`, `TaggedSection` e `IsSnapshot`. Listar no commit qualquer outra linha.

- [ ] **Step 6: Commit**

```bash
git add -A
git commit -m "feat(dotnet): eventos, blocos de conteúdo, StreamJsonParser e TranscriptParser, do CVC 16809aa"
```

---

### Task 5: Os comandos como dados e quem os escreve

**Files:**
- Create: `libs/dotnet/src/ClaudeCliProtocol/ClaudeCommand.cs`
- Create: `libs/dotnet/src/ClaudeCliProtocol/ImageData.cs`
- Create: `libs/dotnet/src/ClaudeCliProtocol/StdinWriter.cs`
- Create: `libs/dotnet/tests/ClaudeCliProtocol.Tests/StdinWriterTests.cs`
- Modify: `libs/dotnet/tests/ClaudeCliProtocol.Tests/PublicApiTests.cs`

**Interfaces:**
- Consumes: `PermissionRequested` (Tarefa 4).
- Produces, em `ClaudeCommand.cs`:

```csharp
public abstract record ClaudeCommand;

public sealed record SendUserMessage(string Text) : ClaudeCommand
{
    public IReadOnlyList<ImageData> Images { get; init; } = Array.Empty<ImageData>();
    public string? Uuid { get; init; }
    public IReadOnlyList<string> InlinePastes { get; init; } = Array.Empty<string>();
}

public enum PermissionDecision { Allow, AllowAlways, Deny }

public sealed record AnswerPermission(string RequestId, PermissionDecision Decision, string? Message = null) : ClaudeCommand;

/// A control request. Whoever sends it sets RequestId; a caller leaves it null.
public abstract record ControlCommand : ClaudeCommand
{
    public string? RequestId { get; init; }
    public abstract string Subtype { get; }
}

public sealed record Initialize : ControlCommand;                        // initialize
public sealed record Interrupt : ControlCommand;                         // interrupt
public sealed record SetPermissionMode(string Mode) : ControlCommand;    // set_permission_mode
public sealed record SetModel(string Model) : ControlCommand;            // set_model
public sealed record ApplyEffort(string? Effort) : ControlCommand;       // apply_flag_settings
public sealed record GetSettings : ControlCommand;                       // get_settings
public sealed record GetContextUsage : ControlCommand;                   // get_context_usage
public sealed record GetTaskOutput(string TaskId) : ControlCommand;      // get_task_output
public sealed record SetRemoteControl(bool Enabled) : ControlCommand;    // remote_control
public sealed record GetChromeStatus : ControlCommand;                   // get_chrome_dialog
```

- Produces, em `ImageData.cs`: `public sealed record ImageData(string MediaType, string Data)`, com `Data` em base64.
- Produces, em `StdinWriter.cs`:

```csharp
public static class StdinWriter
{
    public const string DefaultDenyMessage = "The user denied this tool use.";

    /// One JSON line, without the line feed, ASCII only.
    public static string Write(SendUserMessage message);

    /// Throws ArgumentException when command.RequestId is null.
    public static string Write(ControlCommand command);

    /// The answer carries the input and the suggestions of the request the process side received.
    /// Throws ArgumentException when the two request ids differ.
    public static string Write(AnswerPermission answer, PermissionRequested request);
}
```

O nome é `StdinWriter`, e não `StdinMessages`, para um app que usa o CVC 0.11 e a lib juntos não ter dois `StdinMessages` no mesmo arquivo.

- [ ] **Step 1: Portar os testes**

Copiar `TESTS/StdinMessagesTests.cs` para `StdinWriterTests.cs` e reescrever cada teste na forma nova, mantendo o nome do teste e as mesmas asserções sobre o JSON. A correspondência:

| No CVC | Na lib |
| --- | --- |
| `StdinMessages.UserMessage(text, images)` | `StdinWriter.Write(new SendUserMessage(text) { Images = … })` |
| `StdinMessages.UserMessage(eventArgs)` | `StdinWriter.Write(new SendUserMessage(text) { Uuid = …, InlinePastes = … })` |
| `StdinMessages.PermissionResponse(eventArgs)` | `StdinWriter.Write(new AnswerPermission(id, decision, message), request)` |
| `StdinMessages.SetPermissionMode("req_1", "plan")` | `StdinWriter.Write(new SetPermissionMode("plan") { RequestId = "req_1" })` |
| os outros pedidos de controle | o record de mesmo sentido, com `RequestId` |

O que o teste do CVC confere dos arquivos anexados (`ChatFile.RemotePath`, a linha `[attached file: …]`) fica no CVC: quem monta o texto com `AttachedFileLine.Format` é quem chama. Dizer no commit quais testes ficaram de fora por isso.

Acrescentar:

```csharp
[Fact]
public void A_control_command_without_a_request_id_is_refused()
{
    Assert.Throws<ArgumentException>(() => StdinWriter.Write(new Interrupt()));
}

[Fact]
public void An_answer_for_another_request_is_refused()
{
    var request = Request("req_1"); // helper: a PermissionRequested read from a recorded can_use_tool line
    Assert.Throws<ArgumentException>(() =>
        StdinWriter.Write(new AnswerPermission("req_2", PermissionDecision.Allow), request));
}

[Fact]
public void No_public_member_takes_the_input_or_the_suggestions_from_the_caller()
{
    var properties = typeof(AnswerPermission).GetProperties().Select(p => p.Name).ToList();
    Assert.Equal(new[] { "RequestId", "Decision", "Message" }, properties.Where(n => n != "EqualityContract"));
}

[Fact]
public void Every_line_is_ascii()
{
    var line = StdinWriter.Write(new SendUserMessage("ação “quoted” 日本"));
    Assert.All(line, c => Assert.True(c < 128));
}
```

Em `PublicApiTests`: `ClaudeCommand`, `ControlCommand`, `SendUserMessage`, `AnswerPermission`, `PermissionDecision`, `ImageData` e `StdinWriter` são públicos; `ClaudeCliProtocol.StdinMessages` não existe.

- [ ] **Step 2: Rodar e ver falhar**

Run: `dotnet build libs/dotnet -c Release`
Expected: FAIL de compilação, `StdinWriter` não existe.

- [ ] **Step 3: Escrever os três arquivos**

O corpo de `StdinWriter` é o de `SRC/Protocol/StdinMessages.cs`, lendo dos records em vez dos `EventArgs`. O JSON de cada linha não muda em nada: mesma ordem de campos, mesmo escape.

- [ ] **Step 4: Rodar e ver passar**

Run: `dotnet build libs/dotnet -c Release && dotnet test libs/dotnet -c Release --no-build`
Expected: tudo verde nos dois targets.

- [ ] **Step 5: Commit**

```bash
git add -A
git commit -m "feat(dotnet): comandos como dados (ClaudeCommand) e StdinWriter"
```

---

### Task 6: O livro de pedidos de permissão

**Files:**
- Create: `libs/dotnet/src/ClaudeCliProtocol/PermissionLedger.cs`
- Create: `libs/dotnet/tests/ClaudeCliProtocol.Tests/PermissionLedgerTests.cs`

**Interfaces:**
- Consumes: `ClaudeEvent`, `PermissionRequested`, `PermissionCancelled`, `UserBlock`, `ToolResultBlock`, `TurnCompleted` (Tarefa 4).
- Produces:

```csharp
/// The permission requests the claude sent and nobody answered yet. Thread-safe.
public sealed class PermissionLedger
{
    /// Follows one event. True when the pending requests changed.
    public bool Observe(ClaudeEvent e);

    /// Takes a request out. False when it is not pending: answered, cancelled, resolved or unknown.
    public bool TryTake(string requestId, out PermissionRequested? request);

    /// A copy, in the order the requests arrived.
    public IReadOnlyList<PermissionRequested> Pending { get; }

    public void Clear();
}
```

As regras de `Observe`, as do supervisor de referência (ver a spec):

| Evento | Efeito |
| --- | --- |
| `PermissionRequested` | entra, na ordem de chegada; um `RequestId` repetido substitui o anterior |
| `PermissionCancelled` | sai o pedido com aquele `RequestId` |
| `UserBlock` cujo `Block` é um `ToolResultBlock` | saem os pedidos com aquele `ToolUseId` |
| `TurnCompleted` | saem todos |
| qualquer outro | nada |

- [ ] **Step 1: Escrever os testes que falham**

```csharp
[Fact] public void A_request_is_pending_until_it_is_taken()
[Fact] public void A_request_is_taken_only_once()                 // segundo TryTake: false, request null
[Fact] public void An_unknown_request_is_not_taken()
[Fact] public void A_cancelled_request_leaves()
[Fact] public void A_tool_result_resolves_the_request_of_its_tool_use()
[Fact] public void The_end_of_the_turn_clears_everything()
[Fact] public void Pending_keeps_the_order_of_arrival()
[Fact] public void Pending_is_a_copy()                            // mexer na lista devolvida não muda o livro
[Fact] public void The_taken_request_carries_the_input_and_the_suggestions_the_claude_sent()
```

O último lê a linha `can_use_tool` gravada na fixture `edit` com o `StreamJsonParser`, passa o evento por `Observe`, chama `TryTake` e confere que `Input` e `Suggestions` do pedido devolvido têm o mesmo JSON da linha.

- [ ] **Step 2: Rodar e ver falhar**

Run: `dotnet build libs/dotnet -c Release`
Expected: FAIL de compilação, `PermissionLedger` não existe.

- [ ] **Step 3: Escrever `PermissionLedger`**

Uma lista sob um `lock`; `Pending` devolve uma cópia.

- [ ] **Step 4: Rodar e ver passar**

Run: `dotnet build libs/dotnet -c Release && dotnet test libs/dotnet -c Release --no-build --filter "FullyQualifiedName~PermissionLedgerTests"`
Expected: 9 testes passando em cada target.

- [ ] **Step 5: Commit**

```bash
git add -A
git commit -m "feat(dotnet): PermissionLedger, os pedidos de permissão pendentes e a regra da resposta"
```

---

### Task 7: A interface de transporte e o `ReplayTransport`

**Files:**
- Create: `libs/dotnet/src/ClaudeCliProtocol/IClaudeTransport.cs` (com `TransportState`)
- Create: `libs/dotnet/src/ClaudeCliProtocol/ReplayTransport.cs`
- Create: `libs/dotnet/tests/ClaudeCliProtocol.Tests/ReplayTransportTests.cs`

**Interfaces:**
- Consumes: `ClaudeCommand` (Tarefa 5).
- Produces:

```csharp
public enum TransportState { Open, Dropped, Closed }

public interface IClaudeTransport : IAsyncDisposable
{
    /// The next stream-json line, or null once the transport has closed.
    Task<string?> ReceiveLineAsync(CancellationToken ct);

    /// A command; each transport delivers it in its own way.
    Task SendAsync(ClaudeCommand command, CancellationToken ct);

    /// Raised when the transport drops, comes back or closes.
    event EventHandler<TransportState>? StateChanged;
}

/// Plays a recording: gives its lines in order and ignores every command.
public sealed class ReplayTransport : IClaudeTransport
{
    public ReplayTransport(IEnumerable<string> lines);
    public static ReplayTransport FromFile(string path);
}
```

A interface é a da spec, sem mudança. `Dropped` é o estado de um transporte que caiu e pode voltar (um transporte por rede); o `ReplayTransport` só conhece `Open` e `Closed`.

- [ ] **Step 1: Escrever os testes que falham**

```csharp
[Fact] public async Task It_gives_the_lines_in_order_and_then_null()
[Fact] public async Task Blank_lines_are_skipped_and_a_trailing_carriage_return_is_removed()
[Fact] public async Task After_the_end_it_keeps_giving_null()
[Fact] public async Task Commands_are_ignored()                    // SendAsync de qualquer comando não levanta e não muda as linhas
[Fact] public async Task It_reports_closed_once_at_the_end()       // StateChanged com Closed, uma vez, ao devolver o primeiro null
[Fact] public async Task A_cancelled_token_stops_the_read()        // OperationCanceledException
[Fact] public async Task After_dispose_it_gives_null()
[Fact] public async Task It_reads_a_recording_from_a_file()        // FromFile na fixture "edit": as mesmas linhas de FixtureFiles.StreamLines("edit")
```

- [ ] **Step 2: Rodar e ver falhar**

Run: `dotnet build libs/dotnet -c Release`
Expected: FAIL de compilação, `ReplayTransport` não existe.

- [ ] **Step 3: Escrever os dois arquivos**

`FromFile` lê o arquivo inteiro como UTF-8. `ReceiveLineAsync` não espera nada: devolve a linha na hora.

- [ ] **Step 4: Rodar e ver passar**

Run: `dotnet build libs/dotnet -c Release && dotnet test libs/dotnet -c Release --no-build --filter "FullyQualifiedName~ReplayTransportTests"`
Expected: 8 testes passando em cada target.

- [ ] **Step 5: Commit**

```bash
git add -A
git commit -m "feat(dotnet): IClaudeTransport e ReplayTransport"
```

---

### Task 8: A `ClaudeSession`

**Files:**
- Create: `libs/dotnet/src/ClaudeCliProtocol/ClaudeSession.cs`
- Modify: `libs/dotnet/src/ClaudeCliProtocol/ClaudeEvents.cs` (o evento `ControlReplied`)
- Create: `libs/dotnet/tests/ClaudeCliProtocol.Tests/ScriptedTransport.cs` (um transporte de teste)
- Create: `libs/dotnet/tests/ClaudeCliProtocol.Tests/ClaudeSessionTests.cs`
- Modify: `libs/dotnet/tests/ClaudeCliProtocol.Tests/PublicApiTests.cs` (37 eventos)

**Interfaces:**
- Consumes: `IClaudeTransport`, `TransportState` (Tarefa 7); `StreamJsonParser` e os eventos (Tarefa 4); `ClaudeCommand`, `ControlCommand`, `SendUserMessage`, `AnswerPermission` (Tarefa 5); `PermissionLedger` (Tarefa 6).
- Produces:

```csharp
/// A control request this session sent, with the claude's reply to it.
public sealed record ControlReplied(ControlCommand Command, ControlAcknowledged Reply) : ClaudeEvent;

public sealed class ClaudeSession : IAsyncDisposable
{
    public ClaudeSession(IClaudeTransport transport);

    public string? SessionId { get; }
    public string? PermissionMode { get; }     // confirmed by the claude
    public string? Model { get; }              // confirmed by the claude
    public bool IsBusy { get; }
    public IReadOnlyList<PermissionRequested> PendingPermissions { get; }
    public TransportState TransportState { get; }

    /// Raised after any of the properties above changed.
    public event EventHandler? StateChanged;

    /// The events of the session, until the transport closes. Call it once.
    public IAsyncEnumerable<ClaudeEvent> ReadEventsAsync(CancellationToken ct = default);

    /// False when nothing was sent: an answer to a permission request that is not pending.
    public Task<bool> SendAsync(ClaudeCommand command, CancellationToken ct = default);

    public ValueTask DisposeAsync();           // disposes the transport
}
```

As regras de estado, as do supervisor de referência (ver a spec), cada uma com o seu teste:

| O que acontece | Efeito |
| --- | --- |
| `SendAsync(SendUserMessage)` | abre um turno **antes** de entregar ao transporte; se o transporte levantar exceção, o turno é desfeito e a exceção sobe |
| `SessionStarted` | `SessionId`, `Model` e `PermissionMode` passam a ser os da linha (um valor nulo não apaga o anterior); sem turno aberto, abre um (a mensagem que veio pelo Remote Control) |
| `StatusChanged` com `PermissionMode` não nulo | `PermissionMode` passa a ser esse |
| `TurnCompleted` | fecha um turno, sem passar de zero |
| `IsBusy` | há turno aberto |
| qualquer evento | passa pelo `PermissionLedger`; `PendingPermissions` é o `Pending` dele |
| `SendAsync(AnswerPermission)` | se o pedido está pendente, sai do livro e o comando vai ao transporte; se não, nada vai e o resultado é `false` |
| `SendAsync(ControlCommand)` | a sessão põe um `RequestId` novo (`Guid.NewGuid().ToString("N")`) e **guarda o comando antes** de entregar ao transporte; se a entrega falhar, o registro sai |
| `ControlAcknowledged` de um `RequestId` guardado | depois dele, a sessão devolve um `ControlReplied` com o comando; se não for erro: `SetPermissionMode` confirma o modo, `SetModel` confirma o modelo e, num `Interrupt` com turno aberto, os turnos passam a ser o tamanho de `still_queued` da resposta mais um |
| `ControlAcknowledged` de um `RequestId` desconhecido | passa como veio, sem `ControlReplied` |
| o transporte fecha (`null`) | a leitura termina; turnos em zero; o livro e os controles guardados esvaziam |
| `StateChanged` do transporte | `TransportState` acompanha |

O estado fica sob um `lock`, porque `SendAsync` pode vir de outra thread. `StateChanged` é levantado fora do `lock`.

- [ ] **Step 1: Escrever o transporte de teste**

`ScriptedTransport : IClaudeTransport`: `Enqueue(string line)` põe uma linha na fila; `Close()` faz o próximo `ReceiveLineAsync` devolver `null`; `Sent` é a lista dos comandos recebidos; `OnSend` é um `Action<ClaudeCommand>?` chamado dentro de `SendAsync` antes de ele voltar (é por ele que um teste enfileira a resposta antes de o envio terminar); `FailNextSend` faz o próximo `SendAsync` levantar `IOException`.

- [ ] **Step 2: Escrever os testes que falham**

```csharp
// estado
[Fact] public async Task The_init_line_gives_the_session_id_the_model_and_the_mode()
[Fact] public async Task A_message_makes_the_session_busy_until_its_result()
[Fact] public async Task Two_messages_need_two_results()
[Fact] public async Task An_init_with_no_open_turn_opens_one()
[Fact] public async Task A_result_with_no_open_turn_does_not_go_negative()
[Fact] public async Task A_message_that_cannot_be_sent_does_not_leave_the_session_busy()
[Fact] public async Task State_changed_is_raised_when_busy_changes()

// permissões
[Fact] public async Task A_permission_request_is_pending_until_answered()
[Fact] public async Task An_answer_goes_to_the_transport_as_the_command_it_is()
[Fact] public async Task An_answer_to_a_request_that_is_not_pending_sends_nothing()   // false, transport.Sent vazio; a segunda resposta ao mesmo pedido também

// controle
[Fact] public async Task A_control_command_gets_a_request_id_from_the_session()
[Fact] public async Task The_mode_is_confirmed_only_by_the_reply()
[Fact] public async Task An_error_reply_confirms_nothing()
[Fact] public async Task The_reply_comes_back_with_its_command()                      // ControlReplied logo depois do ControlAcknowledged
[Fact] public async Task A_reply_that_arrives_before_the_send_returns_is_still_matched()
[Fact] public async Task A_reply_nobody_here_asked_for_passes_through_alone()
[Fact] public async Task An_interrupt_reply_counts_what_is_still_queued()

// robustez
[Fact] public async Task A_line_that_is_not_an_event_does_not_stop_the_session()
[Fact] public async Task When_the_transport_closes_mid_turn_the_session_is_idle_with_nothing_pending()

// gravações
[Theory, MemberData(nameof(Scenarios))]
public async Task Every_recording_plays_to_the_end(string scenario)
```

Os valores que fixam os testes menos óbvios:

- `A_reply_that_arrives_before_the_send_returns_is_still_matched`: `OnSend` enfileira `{"type":"control_response","response":{"subtype":"success","request_id":"<o RequestId do comando recebido>","response":{}}}` para um `SetPermissionMode("plan")`. Depois de ler os eventos, `PermissionMode` é `"plan"` e há um `ControlReplied`.
- `An_interrupt_reply_counts_what_is_still_queued`: três mensagens enviadas, um `Interrupt`, a resposta com `"response":{"still_queued":["a"]}`. A sessão continua ocupada depois de um `result` e fica livre depois do segundo.
- `A_line_that_is_not_an_event_does_not_stop_the_session`: as linhas `""`, `"not json"`, `"{\"type\":\"future_type\"}"`, `"{\"type\":\"assistant\",\"mess"` e `"\u001b[2K{\"type\":\"system\",\"subtype\":\"init\",\"session_id\":\"s1\"}"`, seguidas de um `result`. A leitura chega ao `TurnCompleted`, e `SessionId` é `"s1"`.
- `Every_recording_plays_to_the_end`: um `ClaudeSession` sobre `new ReplayTransport(FixtureFiles.StreamLines(scenario))`. No fim: nenhum erro, `SessionId` não nulo, `IsBusy` falso e `PendingPermissions` vazio. Se um cenário terminar de outro jeito, investigar a gravação antes de mexer na asserção, e parar para relatar se a gravação de fato termina ocupada.

- [ ] **Step 3: Rodar e ver falhar**

Run: `dotnet build libs/dotnet -c Release`
Expected: FAIL de compilação, `ClaudeSession` não existe.

- [ ] **Step 4: Escrever `ClaudeSession` e o evento `ControlReplied`**

`ReadEventsAsync` é um iterador assíncrono com `[EnumeratorCancellation]`: lê uma linha, passa pelo `StreamJsonParser`, atualiza o estado a cada evento e o devolve.

- [ ] **Step 5: Rodar e ver passar**

Run: `dotnet build libs/dotnet -c Release && dotnet test libs/dotnet -c Release --no-build`
Expected: tudo verde nos dois targets, com os 21 cenários de `Every_recording_plays_to_the_end`.

- [ ] **Step 6: Commit**

```bash
git add -A
git commit -m "feat(dotnet): ClaudeSession, eventos tipados e estado sobre um transporte"
```

---

### Task 9: O resultado esperado de cada gravação

**Files:**
- Create: `libs/dotnet/tests/ClaudeCliProtocol.Tests/ConformanceJson.cs`
- Create: `libs/dotnet/tests/ClaudeCliProtocol.Tests/ConformanceTests.cs`
- Create: `fixtures/<cenário>.stream.expected.json` e `fixtures/<cenário>.transcript.expected.json` (42 arquivos gerados)
- Modify: `fixtures/README.md` (o formato)

**Interfaces:**
- Consumes: `StreamJsonParser`, `TranscriptParser`, `FixtureFiles`.
- Produces: `ConformanceJson.Write(IEnumerable<(int Line, ClaudeEvent Event)> events) -> string`, no projeto de teste. O formato é o contrato com as libs das outras linguagens; o código que o escreve em .NET não é API.

O formato, versão 1:

```json
{
  "format": 1,
  "events": [
    { "line": 1, "event": "session_started", "fields": { "session_id": "…", "model": "…" } }
  ]
}
```

- `line`: o número da linha na gravação, contando do 1 e sem as linhas em branco.
- `event`: o nome do record em `snake_case` (`AssistantBlock` vira `assistant_block`).
- `fields`: toda propriedade pública que tem `set` ou `init`, com o nome em `snake_case`. Propriedade calculada (só `get`) não entra; propriedade `internal` não entra.
- Valores: texto, número e booleano como são; nulo como `null` (o campo não some); enum pelo nome em `snake_case`; `TimeSpan` em milissegundos inteiros; `DateTimeOffset` em ISO 8601 UTC com milissegundos e `Z`; `JsonElement` como o JSON que ele é (`Undefined` vira `null`); lista como array; record aninhado como objeto, pela mesma regra.
- Bloco de conteúdo: um objeto com `"kind"` (`text`, `thinking`, `tool_use`, `tool_result`, `image`) e os campos dele.
- O `Data` de uma imagem não entra inteiro: vira `{ "length": <caracteres>, "sha256": "<hex minúsculo dos bytes UTF-8 do texto base64>" }`.
- A comparação é estrutural: a ordem das chaves de um objeto não importa, a ordem dos itens de um array importa, e número é comparado pelo valor, com tolerância de `1e-9`.

- [ ] **Step 1: Escrever os testes que falham**

```csharp
[Fact]
public void The_format_follows_its_rules()
{
    // um evento de cada forma que a regra cobre, montado à mão, contra o JSON escrito à mão:
    // texto nulo, enum, DateTimeOffset, JsonElement, lista, bloco de conteúdo e imagem
}

[Theory, MemberData(nameof(Scenarios))]
public void The_stream_recording_gives_the_expected_events(string scenario)

[Theory, MemberData(nameof(Scenarios))]
public void The_transcript_recording_gives_the_expected_events(string scenario)

[Fact]
public void Every_scenario_has_its_two_expected_files()
```

Os dois `Theory` leem a gravação, passam cada linha pelo parser (um `StreamJsonParser` por cenário; `TranscriptParser.Parse` no outro), escrevem com `ConformanceJson.Write` e comparam com o arquivo `.expected.json`. Com a variável de ambiente `CCP_UPDATE_EXPECTED=1`, em vez de comparar, gravam o arquivo em `fixtures/` do repositório (LF, UTF-8 sem BOM, indentado com 2 espaços) e falham dizendo que o arquivo foi escrito.

- [ ] **Step 2: Rodar e ver falhar**

Run: `dotnet build libs/dotnet -c Release`
Expected: FAIL de compilação, `ConformanceJson` não existe.

- [ ] **Step 3: Escrever `ConformanceJson` e fazer o teste do formato passar**

Run: `dotnet build libs/dotnet -c Release && dotnet test libs/dotnet -c Release --no-build --filter "FullyQualifiedName~The_format_follows_its_rules"`
Expected: PASS. Os outros três ainda falham (não há arquivos esperados).

- [ ] **Step 4: Gerar os 42 arquivos**

Run (PowerShell): `$env:CCP_UPDATE_EXPECTED = '1'; dotnet test libs/dotnet -c Release --no-build --framework net6.0 --filter "FullyQualifiedName~ConformanceTests"; Remove-Item Env:CCP_UPDATE_EXPECTED`
Expected: os testes falham dizendo que escreveram os arquivos; `git status` mostra 42 arquivos novos em `fixtures/`.

- [ ] **Step 5: Conferir três à mão**

Os arquivos saem da própria lib, então passar no teste não prova que estão certos. Para `edit`, `denied` e `mode-and-model`, abrir a gravação e o esperado lado a lado e conferir: o `session_started` contra a linha `system/init`; um `permission_requested` contra a linha `can_use_tool` (o `input` inteiro); o `turn_completed` contra a linha `result` (os tokens somados). Anotar no commit quais foram conferidos; os outros 39 ficam como gerados.

- [ ] **Step 6: Rodar e ver passar nos dois targets**

Run: `dotnet build libs/dotnet -c Release && dotnet test libs/dotnet -c Release --no-build`
Expected: tudo verde em `net6.0` e em `net48`. As duas builds da lib dão o mesmo resultado para as 42 gravações.

- [ ] **Step 7: Escrever o formato no `fixtures/README.md`**

As regras acima, em português, com um exemplo tirado do `edit`, e como regenerar (`CCP_UPDATE_EXPECTED=1`).

- [ ] **Step 8: Commit**

```bash
git add -A
git commit -m "test(dotnet): resultado esperado das 42 gravações em JSON neutro, e o teste de conformidade"
```

---

### Task 10: Fechar o pacote e a documentação

**Files:**
- Modify: `libs/dotnet/README.md`
- Create: `libs/dotnet/tests/ClaudeCliProtocol.Tests/ReadmeExamplesTests.cs`
- Modify: `CHANGELOG.md`
- Modify: `README.md`, `README.en.md`, `README.es.md`, `README.zh-cn.md`, `README.zh-tw.md` (a seção **Estado**)
- Modify: `tools/check_package.py` (se a descrição ou as dependências mudaram; não devem ter mudado)

- [ ] **Step 1: Escrever o uso no `libs/dotnet/README.md`**, em inglês, com dois exemplos que compilam:

```csharp
// read a line the claude wrote
var parser = new StreamJsonParser();
foreach (var e in parser.Parse(line)) { /* ... */ }

// play a recording through a session
await using var session = new ClaudeSession(ReplayTransport.FromFile("edit.stream.ndjson"));
await foreach (var e in session.ReadEventsAsync())
{
    if (e is PermissionRequested request)
        await session.SendAsync(new AnswerPermission(request.RequestId, PermissionDecision.Allow));
}
```

E a regra de segurança em um parágrafo. O README continua dizendo que não há release.

- [ ] **Step 2: Provar que os exemplos compilam**

Copiar os dois para um teste `ReadmeExamplesTests.cs` (o segundo sobre a fixture `edit`) e rodar.

Run: `dotnet build libs/dotnet -c Release && dotnet test libs/dotnet -c Release --no-build --filter "FullyQualifiedName~ReadmeExamplesTests"`
Expected: PASS nos dois targets.

- [ ] **Step 3: Atualizar o `CHANGELOG.md` e a seção Estado dos cinco READMEs**

`[Unreleased]` ganha, em `### Added`: os parsers e os eventos (vindos do Claude Visual Chat 0.10.0), `ClaudeCommand` e `StdinWriter`, `PermissionLedger`, `IClaudeTransport` e `ReplayTransport`, `ClaudeSession`, as 42 gravações e o resultado esperado. A seção **Estado** dos READMEs passa a dizer fase 1: a lib lê e escreve o protocolo e passa nas gravações; ainda não sobe o `claude` (fase 2) e não foi publicada.

- [ ] **Step 4: Rodar tudo**

```bash
python -I tools/test_repo.py -v
dotnet build libs/dotnet -c Release
dotnet test libs/dotnet -c Release --no-build
dotnet pack libs/dotnet/src/ClaudeCliProtocol -c Release --no-build -o artifacts
python -I tools/check_package.py artifacts/ClaudeCliProtocol.0.1.0.nupkg
```

Expected: tudo verde; `package ok`.

- [ ] **Step 5: Commit**

```bash
git add -A
git commit -m "docs(dotnet): uso da lib no README do pacote, CHANGELOG e estado da fase 1"
```
