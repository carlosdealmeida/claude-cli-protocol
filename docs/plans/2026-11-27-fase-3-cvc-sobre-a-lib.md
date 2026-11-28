# Fase 3: o CVC sobre a lib — Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** O `ClaudeVisualChat.Avalonia` deixa de ter o protocolo dentro dele e passa a depender do
pacote `ClaudeCliProtocol`. Sai o CVC 0.12.0, sobre a lib 0.2.0.

**Architecture:** São duas entregas, uma em cada repositório, nesta ordem. Primeiro a lib alcança o
CVC de hoje: o `Protocol/` do CVC ganhou tipos depois do commit de onde a lib foi tirada (`16809aa`),
e o CVC não tem como ler o contexto da IDE enquanto `UserBlock.Context` for interno. Depois o CVC
apaga os arquivos que foram para a lib, troca o `using` e fica só com a tela, o `StdinMessages` e a
conversão dos seus `EventArgs` em comandos. O CVC é portado contra um pacote local da lib **antes**
de a lib ser publicada, para que o que faltar entre na 0.2.0 e não numa 0.2.1.

**Tech Stack:** .NET (`netstandard2.0`, `net6.0`, testes do CVC em `net8.0`), xUnit, Avalonia 11.0.10,
NuGet, Python (gravador), Astro + Starlight (site).

**Spec:** [docs/specs/2026-11-22-claude-cli-protocol-design.md](../specs/2026-11-22-claude-cli-protocol-design.md):
"As camadas", "O que vem do CVC", a tabela "Fases" (linha 3) e as decisões 1, 2, 3 e 4.

**Repositórios:** `C:\@work\MyProjects\claude-cli-protocol` (parte A) e
`C:\@work\MyProjects\claude-visual-chat` (parte B).

## O que foi conferido em 2026-11-27

- A lib 0.1.0 é o `Protocol/` do CVC no commit `16809aa`, com o namespace trocado, `SlashCommand` e
  `ModelOption` no lugar de `ChatCommand` e `ChatModel`, e `TaggedMessage`, `TaggedSection` e
  `UsageLimits.IsSnapshot` públicos. Arquivo a arquivo, não há outra diferença.
- O CVC andou 26 commits depois do `16809aa` e está na **0.11.1** (publicada em 2026-11-27, com a
  0.11.0 em 2026-11-25). Nenhuma das duas usa a lib. O que o `Protocol/` dele ganhou e a lib não tem:
  - os blocos `AdvisorUseBlock` e `AdvisorResultBlock` (`server_tool_use` de nome `advisor` e
    `advisor_tool_result`), com o enum `AdvisorOutcome`, que hoje mora em `ChatItems.cs`;
  - o evento `BackgroundTasksChanged` e o record `BackgroundTaskEntry` (`system/background_tasks_changed`);
  - o pedido de controle `stop_task` (`StdinMessages.StopTask`);
  - quatro cenários gravados (`advisor`, `bash-ansi`, `stop-task`, `workflow`) e o que o gravador
    precisou aprender para gravá-los (`ambiente`, `argumentos`, `parar_tarefa_apos_s`);
  - testes: `StreamJsonParserTests` (+65 linhas), `TranscriptParserTests` (+15),
    `StdinMessagesTests` (+9), `FixturePrivacyTests` (+28).
- Fora do `Protocol/`, o código do CVC usa três coisas internas dele: `JsonHelpers` (`Prop`, `Str`,
  `Bool`, `Long`, em `ChatConversation.cs`), `TaskNotificationText` (`Prefix` e `Read`) e
  `UserBlock.Context` com o record `ContextBlock`.
- O `main` do CVC está igual ao `origin/main`, e existe a worktree `.worktrees/v0.11.1` de outra
  sessão. A parte B mexe em mais de 60 arquivos (os `using`): ela só começa com o Carlos dizendo que
  não há outro trabalho aberto no CVC.

## Decisões a confirmar antes de executar

O plano foi escrito com a opção recomendada de cada uma. Elas entram na spec como "Decidido em
2026-11-27 (fase 3)", itens 28 a 33, na Task A6.

28. **A fase 3 são duas versões: lib 0.2.0 e CVC 0.12.0.** A spec fala em "CVC 0.11", número que
    já saiu sem a lib.
29. **`ContextBlock` e `UserBlock.Context` passam a ser públicos na lib.** Isso desfaz uma parte da
    decisão 3. O parser tira os blocos de contexto do texto e os guarda só ali; com a lib em outro
    assembly, o CVC não tem de onde recalculá-los. `ContextBlockReader` continua interno.
30. **`JsonHelpers` e `TaskNotificationText` continuam internos na lib** (decisão 3), e o CVC fica
    com uma cópia interna de cada um: quatro métodos de um, cerca de 60 linhas do outro.
31. **`AdvisorOutcome` vai para a lib, e o CVC apaga o `PermissionDecision` dele** para usar o da
    lib (os três membros são os mesmos). Um host importa os dois namespaces; dois enums com o mesmo
    nome dariam erro de ambiguidade em todo arquivo que os usa.
32. **`ClaudeVisualChat.Protocol.StdinMessages` continua**, com as mesmas assinaturas e as mesmas
    linhas, agora escrito sobre o `StdinWriter` (decisão 1). Os dois `EventArgs` ganham `ToCommand()`,
    que é a conversão que a spec deixa no CVC.
33. **O gravador sai do CVC.** O da lib é o que roda toda semana; os quatro cenários novos e o que o
    gravador aprendeu vão para lá. As gravações de `tests/fixtures` continuam no CVC, como estão,
    porque os testes de tela e o sample as usam. Um cenário novo passa a ser gravado no repositório
    da lib e copiado.

## Global Constraints

- **Dependência por pacote com versão**, nunca por referência de projeto nem por código copiado
  (spec, "As camadas"). O `.csproj` do CVC que vai para o `main` referencia `ClaudeCliProtocol` 0.2.0
  do nuget.org.
- **A lib não ganha nada de tela.** Nenhum pacote novo em `ClaudeCliProtocol.csproj`.
- **Targets:** a lib continua `netstandard2.0;net6.0`; o CVC continua `netstandard2.0`. `LangVersion`
  10, `Nullable` ligado, `TreatWarningsAsErrors`.
- **Nomes do protocolo** nos arquivos esperados em `snake_case`, saídos do `ConformanceJson` (por
  reflexão). Nenhum arquivo esperado é escrito à mão.
- **As linhas do stdin não mudam.** Cada linha que o `StdinMessages` do CVC escreve na 0.11.1 sai
  igual, byte a byte, na 0.12.0.
- **Nada de segredo, nome de usuário ou pasta de quem gravou** nas gravações que vão para `fixtures/`.
- **Máquina lenta:** um `dotnet` por vez, com a saída em `C:\tmp\ccp-*.log`. Site: Node 22.23.5 no
  PATH (`$env:Path = "$env:NVM_HOME\v22.23.5;" + $env:Path`).
- **Commits:** na lib, em português (`feat(dotnet): …`, `docs(site): …`), no branch
  `fase-3-lib-0.2.0`, e nada vai ao `master` sem pull request. No CVC, em inglês, como o histórico
  dele (`feat: …`, `chore: version 0.12.0`), no branch `feat/v0.12.0`, numa worktree
  `.worktrees/v0.12.0` tirada do `origin/main`.
- **Push de tag e release são do Carlos.** A tag `v0.2.0` publica no NuGet e não tem volta.

## Review Focus

1. **Um host com `using ClaudeVisualChat;` e `using ClaudeCliProtocol;` no mesmo arquivo.** Ele
   compila sem erro de nome ambíguo. Teste na Task B3.
2. **A resposta de permissão depois da troca para o `StdinWriter`.** Sai igual quando o `input` é
   `Undefined` (`{}`), quando as sugestões não são uma lista e quando a mensagem da recusa vem em
   branco. Testes na Task B2.
3. **Uma mensagem com contexto da IDE** (`ide_selection`, `terminal`, `browser`), ao vivo e do
   arquivo da sessão. As linhas de contexto continuam aparecendo acima do texto. Teste na Task B1.
4. **Um `server_tool_use` que não é o advisor, e um `advisor_tool_result` de tipo desconhecido ou
   sem conteúdo.** Nenhum dos dois vira bloco, e o parser não levanta exceção. Testes na Task A1.
5. **Um host que passa `null` ao `StdinMessages`** (o id do pedido ou o id da tarefa). Recebe a
   mesma `ArgumentNullException` de hoje, e não a `ArgumentException` do `StdinWriter`. Teste na
   Task B2.

---

## Parte A: a lib 0.2.0 (`claude-cli-protocol`)

A fonte de cada trecho é o CVC: `git -C C:\@work\MyProjects\claude-visual-chat diff 16809aa origin/main -- <arquivo>`.
O código vem de lá com o namespace `ClaudeCliProtocol`; os testes vêm do mesmo diff dos arquivos de
teste.

### Task A1: Os blocos do advisor

**Files:**
- Modify: `libs/dotnet/src/ClaudeCliProtocol/ContentBlocks.cs`
- Test: `libs/dotnet/tests/ClaudeCliProtocol.Tests/StreamJsonParserTests.cs`, `TranscriptParserTests.cs`

**Interfaces:**
- Produces: `public enum AdvisorOutcome { Reviewed, Declined, Unavailable }`;
  `public sealed record AdvisorUseBlock(string Id) : ContentBlock`;
  `public sealed record AdvisorResultBlock(string ToolUseId, AdvisorOutcome Outcome) : ContentBlock`
  com `string? Text { get; init; }` e `string? ErrorCode { get; init; }`.

- [ ] **Step 1:** Trazer os testes do advisor do CVC (o diff de `Protocol/StreamJsonParserTests.cs` e
  de `Protocol/TranscriptParserTests.cs`) e somar os do item 4 do Review Focus:
  `server_tool_use` de nome `web_search` não dá bloco; `advisor_tool_result` sem `content` não dá
  bloco; `content.type` desconhecido não dá bloco.
- [ ] **Step 2:** Rodar e ver falhar na compilação.
  `dotnet test libs/dotnet -c Release --framework net6.0 --filter "FullyQualifiedName~ParserTests" > C:\tmp\ccp-a1.log`
- [ ] **Step 3:** Trazer o enum e os dois records, e os dois `case` do `BlockReader` com
  `ReadAdvisorResult`. `advisor_result` com `stop_reason` `refusal` é `Declined`, senão `Reviewed`;
  `advisor_redacted_result` é `Reviewed` sem texto; `advisor_tool_result_error` é `Unavailable` com
  `error_code`.
- [ ] **Step 4:** Rodar de novo: passa.
- [ ] **Step 5:** Commit `feat(dotnet): os blocos do advisor`.

### Task A2: As tarefas em segundo plano

**Files:**
- Modify: `libs/dotnet/src/ClaudeCliProtocol/ClaudeEvents.cs`, `TranscriptParser.cs` (o `EventReader`)
- Test: `StreamJsonParserTests.cs`

**Interfaces:**
- Produces: `public sealed record BackgroundTaskEntry(string TaskId, string TaskType, string Description)`;
  `public sealed record BackgroundTasksChanged(IReadOnlyList<BackgroundTaskEntry> Tasks) : ClaudeEvent`.

- [ ] **Step 1:** Trazer os testes do CVC: uma linha `system/background_tasks_changed` com duas
  tarefas dá um evento com as duas, na ordem; uma entrada sem `task_id` é pulada; `tasks` vazio dá
  lista vazia; sem `tasks` não há evento.
- [ ] **Step 2:** Rodar e ver falhar.
- [ ] **Step 3:** Trazer os dois records e o `case "background_tasks_changed"` com `ReadBackgroundTasks`.
- [ ] **Step 4:** Rodar: passa.
- [ ] **Step 5:** Commit `feat(dotnet): o evento das tarefas em segundo plano`.

### Task A3: O comando `StopTask`

**Files:**
- Modify: `libs/dotnet/src/ClaudeCliProtocol/ClaudeCommand.cs`, `StdinWriter.cs`
- Test: `StdinWriterTests.cs`, `ClaudeSessionTests.cs`

**Interfaces:**
- Produces: `public sealed record StopTask(string TaskId) : ControlCommand` com `Subtype` `"stop_task"`.

- [ ] **Step 1:** Testes: `StdinWriter.Write(new StopTask("t1") { RequestId = "req_1" })` é
  `{"type":"control_request","request_id":"req_1","request":{"subtype":"stop_task","task_id":"t1"}}`;
  `TaskId` nulo levanta `ArgumentNullException`, como o `GetTaskOutput`; pela `ClaudeSession`, a
  resposta volta num `ControlReplied` cujo `Command` é o `StopTask` enviado.
- [ ] **Step 2:** Rodar e ver falhar.
- [ ] **Step 3:** O record e o `case StopTask` no `Write(ControlCommand)`.
- [ ] **Step 4:** Rodar: passa.
- [ ] **Step 5:** Commit `feat(dotnet): o comando StopTask`.

### Task A4: O contexto da IDE vira API pública

**Files:**
- Modify: `libs/dotnet/src/ClaudeCliProtocol/ContextBlocks.cs` (`ContextBlock` público),
  `ClaudeEvents.cs` (`UserBlock.Context` público)
- Test: `PublicApiTests.cs`, `ConformanceTests.cs`
- Modify: `fixtures/*.expected.json` (só se o `ConformanceJson` passar a escrever o campo)

- [ ] **Step 1:** Em `PublicApiTests`: `ContextBlock` é exportado, `UserBlock.Context` é uma
  propriedade pública de tipo `IReadOnlyList<ContextBlock>`, e `ContextBlockReader` não é exportado.
- [ ] **Step 2:** Rodar e ver falhar.
- [ ] **Step 3:** Trocar os dois `internal` por `public`.
- [ ] **Step 4:** Rodar a suíte inteira. Se os `ConformanceTests` falharem porque o `user_block`
  ganhou o campo `context`, reescrever os esperados
  (`$env:CCP_UPDATE_EXPECTED='1'`, o comando do `fixtures/README.md`), ler o diff e conferir que a
  única mudança é esse campo. Nesse caso o campo entra na tabela de `spec/eventos` na Task A6.
- [ ] **Step 5:** Commit `feat(dotnet): o contexto da IDE de uma mensagem é público`.

### Task A5: As quatro gravações e o gravador

**Files:**
- Create: `fixtures/{advisor,bash-ansi,stop-task,workflow}.{stream.ndjson,transcript.jsonl}` (copiados
  de `tests/fixtures` do CVC) e os oito `.expected.json` (gerados)
- Modify: `tools/gravar.py`, `tools/cenarios.json`, `tools/test_gravar.py`,
  `libs/dotnet/tests/ClaudeCliProtocol.Tests/FixturePrivacyTests.cs`

- [ ] **Step 1:** Trazer para `tools/test_gravar.py` os testes que o CVC ganhou (o diff de
  `tools/test_gravar.py`): `ambiente` e `argumentos` do cenário, o passo `parar_tarefa_apos_s`, as
  esperas `advisor`, `tarefa:local_workflow` e `tarefa_parada`. Rodar
  `python -I tools/test_gravar.py` e ver falhar.
- [ ] **Step 2:** Trazer o mesmo para `tools/gravar.py`, por cima do que a fase 2 mudou nele (os dois
  arquivos já diferem em 110 linhas; aplicar o diff do CVC, não copiar o arquivo), e os quatro
  cenários para `tools/cenarios.json`. Rodar: passa.
- [ ] **Step 3:** Trazer o diff de `FixturePrivacyTests.cs` do CVC. Copiar as oito gravações. Rodar
  os testes de privacidade: passam com as gravações novas.
- [ ] **Step 4:** Gerar os oito esperados com `CCP_UPDATE_EXPECTED=1` e ler cada um: o `advisor` tem
  `advisor_use_block` e `advisor_result_block`; o `workflow` e o `stop-task` têm
  `background_tasks_changed`.
- [ ] **Step 5:** Rodar a suíte inteira nos dois targets (`net6.0`, depois `net48`).
- [ ] **Step 6:** Commit `feat(fixtures): os cenários advisor, bash-ansi, stop-task e workflow`.

### Task A6: A especificação, os textos e a spec

**Files:**
- Modify: `site/src/content/docs/spec/{eventos,protocolo,controle,fixtures}.md` e as quatro
  traduções de cada uma; `fixtures/README.md`; `README.md` e traduções (estado e roteiro);
  `CHANGELOG.md` (`Unreleased`); `docs/specs/2026-11-22-claude-cli-protocol-design.md` (decisões 28 a
  33 e a linha 3 da tabela de fases); `libs/dotnet/README.md`

- [ ] **Step 1:** Rodar `python -I tools/test_site_spec.py` e ver falhar: a tabela de eventos não
  tem o evento novo.
- [ ] **Step 2:** Em português: `spec/eventos` ganha `background_tasks_changed` (e o campo `context`
  do `user_block`, se a Task A4 o trouxe); `spec/protocolo` ganha os dois blocos do advisor;
  `spec/controle` ganha o `stop_task`, com a linha de exemplo saída do `StdinWriter`; `spec/fixtures`
  passa de 21 para 25 cenários. Cada linha de exemplo aponta a gravação e a linha
  (`<!-- fixture: <arquivo>:<linha> -->`).
- [ ] **Step 3:** As quatro traduções, uma língua por commit, com o glossário de `docs/i18n/`.
- [ ] **Step 4:** `python -I tools/test_site_spec.py` e `cd site && npm ci && npm run build && npm run check`: passam.
- [ ] **Step 5:** Os outros textos e as decisões na spec. Commit `docs: a lib alcança o CVC 0.11.1`.

### Task A7: O pacote local e o pull request

- [ ] **Step 1:** `dotnet pack libs/dotnet/src/ClaudeCliProtocol -c Release -p:Version=0.2.0-dev.1 -o C:\tmp\ccp-feed`.
  A versão do `.csproj` continua 0.1.0 neste branch.
- [ ] **Step 2:** `python -I tools/check_package.py` no pacote gerado.
- [ ] **Step 3:** Abrir o pull request do `fase-3-lib-0.2.0`. **Parada: o Carlos lê e junta.** A tag
  ainda não.

---

## Parte B: o CVC 0.12.0 (`claude-visual-chat`)

**Antes de começar:** o Carlos confirma que não há outro trabalho aberto no CVC. A worktree nasce de
`origin/main` depois de um `git fetch`. Durante a parte B, o pacote vem da pasta local:

```powershell
$env:ClaudeCliProtocolVersion = '0.2.0-dev.1'
$env:RestoreAdditionalProjectSources = 'C:\tmp\ccp-feed'
```

Se a parte B achar algo que falta na lib, a correção vai para o `master` da lib por pull request, o
pacote local sai de novo como `0.2.0-dev.2` e a variável muda.

### Task B1: O CVC compila sobre o pacote

**Files:**
- Modify: `Directory.Build.props` (`<ClaudeCliProtocolVersion Condition="'$(ClaudeCliProtocolVersion)' == ''">0.2.0</ClaudeCliProtocolVersion>`)
- Modify: `src/ClaudeVisualChat.Avalonia/ClaudeVisualChat.Avalonia.csproj`
  (`<PackageReference Include="ClaudeCliProtocol" Version="$(ClaudeCliProtocolVersion)" />`)
- Delete (`git rm`, arquivo por arquivo): em `src/ClaudeVisualChat.Avalonia/Protocol/`, `AnsiText.cs`,
  `AttachedFileLine.cs`, `ClaudeEvents.cs`, `ContentBlocks.cs`, `ContextBlocks.cs`, `LightMarkup.cs`,
  `StreamJsonParser.cs`, `TaggedMessage.cs`, `TranscriptParser.cs`; e `src/ClaudeVisualChat.Avalonia/ChatCatalog.cs`
- Delete: em `tests/ClaudeVisualChat.Tests/`, `Protocol/StreamJsonParserTests.cs`,
  `Protocol/TranscriptParserTests.cs`, `TaggedMessageTests.cs`, `ContextBlockReaderTests.cs`,
  `AnsiTextTests.cs` (o que eles provam está nos testes da lib desde a parte A)
- Create: `src/ClaudeVisualChat.Avalonia/JsonHelpers.cs`, `src/ClaudeVisualChat.Avalonia/TaskNotificationText.cs`
- Modify: `ChatItems.cs` (sai o `AdvisorOutcome`), `ChatEvents.cs` (sai o `PermissionDecision`), e
  todo arquivo de `src/`, `tests/` e `samples/` com `using ClaudeVisualChat.Protocol;`, `ChatCommand`
  ou `ChatModel`

**Interfaces:**
- Consumes: os tipos públicos de `ClaudeCliProtocol` 0.2.0, entre eles `SlashCommand`, `ModelOption`,
  `PermissionDecision`, `AdvisorOutcome`, `ContextBlock`, `UserBlock.Context`, `TaggedMessage`.
- Produces: `internal static class JsonHelpers` (namespace `ClaudeVisualChat`) com
  `JsonElement? Prop(JsonElement, string)`, `string? Str(JsonElement, string)`,
  `bool Bool(JsonElement, string)` e `long? Long(JsonElement, string)`;
  `internal static class TaskNotificationText` com `const string Prefix` e
  `AgentTaskFinished? Read(string text)`. Os dois são cópia do que a lib tem interno.

- [ ] **Step 1:** Um teste novo em `ChatConversationTests.cs`,
  `A_message_with_ide_context_keeps_its_context_lines`: a conversa recebe uma linha `user` cujo
  conteúdo tem um item `<ide_selection>` antes do texto, e o `UserMessageItem` fica com uma linha de
  contexto e o texto sem o bloco. Rodar na 0.11.1: passa (ele fixa o comportamento antes da troca).
- [ ] **Step 2:** A referência ao pacote, os `git rm`, as duas cópias internas.
- [ ] **Step 3:** Trocar `using ClaudeVisualChat.Protocol;` por `using ClaudeCliProtocol;` (ficam os
  dois onde o arquivo usa `StdinMessages`), `ChatCommand` por `SlashCommand` e `ChatModel` por
  `ModelOption`. Só os tipos: `ChatConversation.Commands`, `AvailableModels` e os outros nomes de
  membro não mudam.
- [ ] **Step 4:** `dotnet build -c Release > C:\tmp\ccp-b1-build.log`: zero erro, zero aviso. O
  `StdinMessages.cs` compila nesta task com o mínimo: `ChatImage` continua o tipo das imagens dele.
- [ ] **Step 5:** `dotnet test -c Release --no-build > C:\tmp\ccp-b1-test.log`: tudo passa, menos os
  `PublicApiTests` que citam `ClaudeVisualChat.Protocol.*`, que são da Task B3. Anotar a contagem.
- [ ] **Step 6:** Commit `feat: the protocol comes from the ClaudeCliProtocol package`.

### Task B2: O `StdinMessages` sobre o `StdinWriter` e os comandos

**Files:**
- Modify: `src/ClaudeVisualChat.Avalonia/Protocol/StdinMessages.cs`, `src/ClaudeVisualChat.Avalonia/ChatEvents.cs`
- Test: `tests/ClaudeVisualChat.Tests/StdinMessagesTests.cs`

**Interfaces:**
- Produces: `public SendUserMessage MessageSubmittedEventArgs.ToCommand()` (o texto com uma linha
  `AttachedFileLine.Format` por arquivo que tem `RemotePath`, as imagens como `ImageData`, o `Uuid`
  e os `InlinePastes`); `public AnswerPermission PermissionAnsweredEventArgs.ToCommand()`
  (`Request.RequestId`, `Decision`, `Message`). As assinaturas públicas do `StdinMessages` ficam
  como estão, com `DefaultDenyMessage`.

- [ ] **Step 1:** Antes de mexer, somar ao `StdinMessagesTests` os casos do Review Focus que ainda
  não estiverem lá, com a linha esperada inteira: permitir com `Input` `Undefined` escreve
  `"updatedInput":{}`; "permitir sempre" com sugestões que não são lista não escreve
  `updatedPermissions`; recusar com mensagem em branco escreve `DefaultDenyMessage`; `requestId`
  nulo em `SetModel`, `GetTaskOutput` e `StopTask` levanta `ArgumentNullException` de nome
  `requestId`; `taskId` nulo levanta `ArgumentNullException` de nome `taskId`. Rodar: passam, com a
  implementação de hoje.
- [ ] **Step 2:** Testes dos dois `ToCommand()`: um `MessageSubmittedEventArgs` com texto, uma
  imagem, um arquivo com `RemotePath` e um `Uuid` dá um `SendUserMessage` com esses dados;
  `StdinWriter.Write(e.ToCommand())` é igual a `StdinMessages.UserMessage(e)`. Rodar: falham.
- [ ] **Step 3:** Escrever os dois `ToCommand()` e reescrever o `StdinMessages` sobre o `StdinWriter`:
  cada método monta o comando da lib com o `RequestId` recebido e chama `StdinWriter.Write`. As
  conferências de nulo ficam no `StdinMessages`, antes da chamada. `PermissionResponse` monta um
  `PermissionRequested` com `RequestId`, `ToolName`, `DisplayName`, `Description`, `ToolUseId`,
  `Input` e `Suggestions` do `PendingPermission` e chama `StdinWriter.Write(AnswerPermission, PermissionRequested)`.
  Nenhum `Utf8JsonWriter` sobra no arquivo.
- [ ] **Step 4:** Rodar o `StdinMessagesTests` inteiro: passa sem mudar nenhuma linha esperada.
- [ ] **Step 5:** Commit `feat: StdinMessages writes through the library, and the events give their command`.

### Task B3: Os nomes públicos

**Files:**
- Modify: `tests/ClaudeVisualChat.Tests/PublicApiTests.cs`

- [ ] **Step 1:** Testes novos:
  - `The_protocol_namespace_keeps_only_StdinMessages`: o único tipo exportado em
    `ClaudeVisualChat.Protocol` é `StdinMessages`;
  - `No_public_type_shares_its_name_with_a_protocol_type`: nenhum `Name` exportado pelo assembly do
    CVC é o de um tipo exportado pelo `ClaudeCliProtocol`;
  - `No_protocol_type_shares_its_name_with_an_avalonia_type`: o mesmo entre a lib e os dois
    assemblies do Avalonia que o teste de hoje já lê;
  - `The_library_depends_on_the_protocol_package`: `GetReferencedAssemblies()` tem `ClaudeCliProtocol`.
- [ ] **Step 2:** Nos testes que existem, `typeof(ClaudeVisualChat.Protocol.X)` vira
  `typeof(ClaudeCliProtocol.X)`, e as listas de nomes exportados perdem os tipos que saíram
  (`ChatCommand`, `ChatModel`, `ContextUsage`, `ContextCategory`, `UsageLimits`, `UsageWindow` e os
  `ClaudeVisualChat.Protocol.*` que não são o `StdinMessages`). A asserção sobre
  `TaskNotificationText` passa a ser que `ClaudeVisualChat.TaskNotificationText` não é exportado.
- [ ] **Step 3:** `dotnet test -c Release > C:\tmp\ccp-b3.log`: a suíte inteira passa. Se o terceiro
  teste novo falhar, o nome que colide é um problema da lib: volta para a parte A antes da tag.
- [ ] **Step 4:** Commit `test: the public names after the protocol moved out`.

### Task B4: O gravador sai do CVC

**Files:**
- Delete: `tools/gravar.py`, `tools/cenarios.json`, `tools/test_gravar.py`
- Modify: `.github/workflows/ci.yml` (saem "Test the recorder" e o `setup-python`), `.gitignore`
  (saem `__pycache__/` e `.pytest_cache/` se nada mais usar Python)

- [ ] **Step 1:** Conferir que os 25 cenários de `tests/fixtures` são os 25 de `fixtures/` da lib, por
  nome. O conteúdo pode diferir (a lib regravou 20 deles no 2.1.331); as gravações do CVC não mudam
  nesta fase.
- [ ] **Step 2:** Os `git rm` e o workflow. `FixturePrivacyTests` e `FixtureFiles` ficam.
- [ ] **Step 3:** `dotnet test -c Release`: passa.
- [ ] **Step 4:** Commit `chore: the recorder lives in claude-cli-protocol`.

### Task B5: Os textos e a versão

**Files:**
- Modify: `README.md`, `README.pt-BR.md`, `CHANGELOG.md`,
  `src/ClaudeVisualChat.Avalonia/ClaudeVisualChat.Avalonia.csproj` (`<Version>0.12.0</Version>`),
  `samples/ClaudeVisualChat.Sample/MainWindow.axaml.cs` (os comentários que citam o `Protocol`)

- [ ] **Step 1:** Os dois READMEs: cada trecho de código compila com os nomes novos; uma seção curta
  diz que o protocolo vem do `ClaudeCliProtocol`, com o link, e que um host que sobe o `claude` pode
  usar o `ProcessTransport` e a `ClaudeSession` dele.
- [ ] **Step 2:** `CHANGELOG.md`, `[0.12.0]`, em "Changed", com a tabela de migração:

  | Antes (0.11) | Agora (0.12) |
  |---|---|
  | `using ClaudeVisualChat.Protocol;` (parsers, eventos, blocos) | `using ClaudeCliProtocol;` |
  | `ChatCommand` | `SlashCommand` |
  | `ChatModel` | `ModelOption` |
  | `ClaudeVisualChat.PermissionDecision`, `ClaudeVisualChat.AdvisorOutcome` | os de `ClaudeCliProtocol` |
  | `ContextUsage`, `ContextCategory`, `ContextFile`, `ContextAgent`, `UsageLimits`, `UsageWindow`, `AccountInfo`, `AuthConflict` em `ClaudeVisualChat` | os mesmos nomes em `ClaudeCliProtocol` |
  | `ClaudeVisualChat.Protocol.StdinMessages` | não muda |

  E em "Added": `ToCommand()` nos dois `EventArgs`; a dependência do pacote `ClaudeCliProtocol`.
- [ ] **Step 3:** `dotnet pack src/ClaudeVisualChat.Avalonia -c Release -o C:\tmp\cvc-pack` e abrir o
  `.nuspec` do pacote: a dependência `ClaudeCliProtocol` está no grupo `.NETStandard2.0`.
- [ ] **Step 4:** Rodar o sample e clicar por uma gravação: a conversa desenha, e o painel da direita
  mostra as linhas do stdin.
- [ ] **Step 5:** Commit `chore: version 0.12.0`.

---

## Fecho: publicar, nesta ordem

- [ ] **A lib 0.2.0.** No `master` da lib: `<Version>0.2.0</Version>`, o `Unreleased` do CHANGELOG
  vira `[0.2.0]`, commit `chore: versão 0.2.0` por pull request. **Parada: o Carlos confirma e
  empurra a tag `v0.2.0`**, que publica no NuGet.
- [ ] **O CVC contra o nuget.org.** Numa sessão sem as duas variáveis de ambiente:
  `dotnet build -c Release` e `dotnet test -c Release` restauram o `ClaudeCliProtocol` 0.2.0 do
  nuget.org e passam. O `0.2.0-dev.N` do cache não é usado, porque a versão pedida é outra.
- [ ] **O CVC 0.12.0.** O CI do branch passa nos dois sistemas. **Parada: o Carlos junta no `main` e
  empurra a tag `v0.12.0`.**
- [ ] **Depois.** No `README.md` da lib e nas traduções, a fase 3 entra como feita. A worktree
  `.worktrees/v0.12.0` e os branches juntados saem com o `limpar.py`.

## Fora desta fase

- Trocar as gravações do CVC pelas regravadas da lib. Os testes de tela dependem do conteúdo delas.
- O sample do CVC subir um `claude` de verdade, ou tocar as gravações pelo `ReplayTransport`.
- Tirar o `StdinMessages` do CVC ou marcá-lo como obsoleto.
- A fase 4 (o aplicativo) e qualquer mudança no supervisor de referência.
- Os itens 🔍 do ROADMAP.
