# Fase 2: o transporte que sobe o `claude` — Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** A lib passa a subir o `claude -p` e a falar com ele: o `ProcessTransport`, um exemplo de console que conversa de verdade, e o gravador das fixtures com a regravação semanal pronta para rodar.

**Architecture:** O `ProcessTransport` é o lado do processo: monta a linha de comando a partir de opções conferidas, lê o stdout linha a linha, lê e descarta o stderr, e escreve no stdin as linhas do `StdinWriter`. Ele tem o seu próprio `PermissionLedger`, alimentado só pelas linhas que ele mesmo leu, e é dele que sai o pedido de cada resposta de permissão: é aqui que a regra da resposta de permissão se cumpre. Os testes não usam o `claude`: usam um executável de mentira (`FakeClaude`), que mostra os argumentos e o ambiente que recebeu, devolve cada linha que chega no stdin e segue um roteiro. Um teste com o `claude` de verdade existe, mas só roda com `CCP_LIVE_CLAUDE=1`. O gravador vem do Claude Visual Chat, em Python, como está.

**Tech Stack:** C# 10, `netstandard2.0;net6.0`, `System.Diagnostics.Process`, xunit 2.9.2; Python 3 (só biblioteca padrão) para o gravador.

**Spec:** [docs/specs/2026-11-22-claude-cli-protocol-design.md](../specs/2026-11-22-claude-cli-protocol-design.md), seções "Os transportes", "A regra de segurança da resposta de permissão", "O que o `ProcessTransport` espelha do supervisor de referência" e as decisões 9 e 10. Os itens 1 a 9 do [ROADMAP](../ROADMAP.md) são da varredura de 2026-11-24.

## Global Constraints

- **Argumentos do `claude`, nesta ordem:** `-p --input-format stream-json --output-format stream-json --verbose --include-partial-messages --permission-prompt-tool stdio --permission-mode <modo>`, e depois, só quando pedidos, `--resume <id>`, `--model <modelo>`, `--effort <nível>`. Nunca `--setting-sources`, e nenhum argumento que não esteja nesta lista.
- **Conferência do que vai à linha de comando** (no Windows um `claude.cmd` passa pelo `cmd.exe`):
  - modo: só `plan`, `default`, `acceptEdits` ou `auto`;
  - modelo: `^[A-Za-z0-9][A-Za-z0-9._:@/\[\]-]{0,99}$`;
  - id de sessão: `^[A-Za-z0-9][A-Za-z0-9_-]{0,199}$`;
  - effort: só `low`, `medium`, `high`, `xhigh` ou `max`.
  Um valor recusado levanta `ArgumentException` antes de qualquer processo subir.
- **Ambiente do processo:** o desta máquina, mais `NoDefaultCurrentDirectoryInExePath=1`, mais as variáveis das opções (uma variável com valor nulo é removida). O transporte não conhece nenhuma variável pelo nome além dessa.
- **stdin:** uma linha do `StdinWriter` por comando, em bytes ASCII, terminada em `\n`, com flush. O stdin só é fechado no encerramento.
- **stdout:** UTF-8, linha a linha. **stderr:** lido e descartado; só a contagem de linhas fica.
- **A regra da resposta de permissão:** a resposta sai do pedido que o `PermissionLedger` do próprio transporte guardou. Um `AnswerPermission` de pedido que não está pendente não escreve nada.
- **Nada em disco:** o transporte não grava a conversa, o stderr nem credenciais.
- **Testes:** `net6.0` e, no Windows, `net48`. Nenhum teste da suíte padrão sobe o `claude` de verdade nem usa a rede.
- **Um `dotnet` de cada vez.** Comandos, da raiz: `dotnet build libs/dotnet -c Release`, `dotnet test libs/dotnet -c Release --no-build`.
- **Idioma:** código, nomes e comentários em inglês; documentação em português, com as traduções onde já existem.
- **Nomes:** nenhum nome de projeto privado, de pessoa ou caminho de máquina em arquivo versionado.
- **Commits:** um por tarefa, mensagem em português. Branch `fase-2-processo`; nada vai ao `master` sem pull request.
- **Fora desta fase:** publicar no NuGet (depende do Trusted Publishing, que é do dono do projeto); o CVC sobre a lib (fase 3); a lista de permissões por pasta (`--add-dir`), o `--chrome` e o `--settings`, que o supervisor de referência tem e a spec não pede.

## Review Focus

1. **Um valor que vira outro comando na linha de comando:** modelo `sonnet & calc`, id de sessão `a" --dangerously-skip-permissions`, modo `bypassPermissions`. Nada sobe. Teste `Values_that_could_become_another_command_are_refused` (Tarefa 2).
2. **A resposta de permissão com outro `input`:** um `AnswerPermission` chega com o id certo, e o transporte escreve o `input` que ele leu do `claude`, não outro; a segunda resposta e a resposta a um pedido cancelado não escrevem nada. Testes `The_answer_carries_the_input_this_transport_read` e `A_second_answer_writes_nothing` (Tarefa 5).
3. **O `claude` que morre na partida ou no meio:** a leitura devolve `null`, o estado vai a `Closed`, o código de saída fica disponível, e um `SendAsync` depois disso não trava nem levanta exceção de pipe. Testes `A_process_that_exits_at_once_closes_the_transport_with_its_exit_code` e `Sending_after_the_process_ended_does_nothing` (Tarefas 4 e 5).
4. **O stderr que enche:** o processo escreve muito no stderr e nada trava, porque alguém o lê. Teste `A_flood_on_stderr_does_not_block_the_process` (Tarefa 4).
5. **O processo que fica para trás:** depois do `DisposeAsync`, o processo e os filhos dele não existem mais. Teste `Dispose_ends_the_process` (Tarefa 4).

---

### Task 1: O `claude` de mentira para os testes

**Files:**
- Create: `libs/dotnet/tests/FakeClaude/FakeClaude.csproj`, `libs/dotnet/tests/FakeClaude/Program.cs`
- Modify: `libs/dotnet/ClaudeCliProtocol.sln`, `libs/dotnet/tests/ClaudeCliProtocol.Tests/ClaudeCliProtocol.Tests.csproj`
- Create: `libs/dotnet/tests/ClaudeCliProtocol.Tests/FakeClaudeHost.cs`, `libs/dotnet/tests/ClaudeCliProtocol.Tests/FakeClaudeTests.cs`

**Interfaces:**
- Produces: `FakeClaude`, um console `net6.0` (com apphost: `FakeClaude.exe` no Windows, `FakeClaude` no Linux), copiado para `fake-claude/` ao lado do assembly de teste dos dois targets.
- Produces: `FakeClaudeHost.Path` (o caminho do executável) e `FakeClaudeHost.Script(params string[] lines)` (grava um roteiro num arquivo temporário e devolve o caminho).
- O comportamento do `FakeClaude`:
  1. Escreve no stdout uma linha `{"type":"fake_start","args":[…],"cwd":"…","env":{…}}`, com os argumentos como chegaram, a pasta atual e só as variáveis de ambiente cujo nome começa com `FAKE_`, `CLAUDE_` ou é `NoDefaultCurrentDirectoryInExePath`.
  2. Se `FAKE_CLAUDE_STDERR_LINES` for um número, escreve essa quantidade de linhas de 200 caracteres no stderr.
  3. Se `FAKE_CLAUDE_SCRIPT` apontar para um arquivo, escreve cada linha dele no stdout.
  4. Se `FAKE_CLAUDE_EXIT_NOW` for um número, sai com esse código sem ler o stdin.
  5. Senão, lê o stdin: para cada linha, escreve `{"type":"fake_stdin","line":"<a linha>"}`; a linha `{"type":"fake_exit","code":N}` faz sair com `N`; o fim do stdin faz sair com 0.
  6. Se `FAKE_CLAUDE_CHILD` for `1`, antes de ler o stdin sobe um filho de si mesmo (com `FAKE_CLAUDE_CHILD` vazio) e escreve `{"type":"fake_child","pid":N}`.

- [ ] **Step 1: Escrever os testes que falham** (`FakeClaudeTests`, com `System.Diagnostics.Process` direto, sem a lib)

```csharp
[Fact] public void It_reports_its_arguments_its_folder_and_the_chosen_environment()
[Fact] public void It_plays_its_script_and_echoes_what_it_reads()
[Fact] public void It_exits_with_the_code_it_is_told()
```

- [ ] **Step 2: Rodar e ver falhar** — `dotnet build libs/dotnet -c Release`: FAIL, `FakeClaudeHost` não existe.
- [ ] **Step 3: Criar o projeto, ligar na solução e copiar o executável para a saída dos testes** (um `ProjectReference` com `ReferenceOutputAssembly="false"` e um alvo que copia a pasta de saída do `FakeClaude` para `fake-claude/`).
- [ ] **Step 4: Rodar e ver passar** nos dois targets.
- [ ] **Step 5: Commit** — `test(dotnet): FakeClaude, o claude de mentira dos testes do transporte`

---

### Task 2: As opções e a linha de comando

**Files:**
- Create: `libs/dotnet/src/ClaudeCliProtocol/ProcessTransportOptions.cs`, `libs/dotnet/src/ClaudeCliProtocol/ClaudeArguments.cs`
- Create: `libs/dotnet/tests/ClaudeCliProtocol.Tests/ClaudeArgumentsTests.cs`

**Interfaces:**
- Produces:

```csharp
public sealed record ProcessTransportOptions(string WorkingDirectory)
{
    /// The claude executable; null finds `claude` on the PATH.
    public string? ClaudePath { get; init; }
    public string PermissionMode { get; init; } = "default";
    public string? ResumeSessionId { get; init; }
    public string? Model { get; init; }
    public string? Effort { get; init; }
    /// Variables added to this machine's environment; a null value removes the variable.
    public IReadOnlyDictionary<string, string?> Environment { get; init; }  // empty by default
    /// Sends `initialize` as soon as the process starts. True by default.
    public bool SendInitialize { get; init; } = true;
}

public static class ClaudeArguments
{
    public static readonly IReadOnlyList<string> PermissionModes;  // plan, default, acceptEdits, auto
    public static readonly IReadOnlyList<string> EffortLevels;     // low, medium, high, xhigh, max
    /// The arguments for these options, in the fixed order. Throws ArgumentException for a refused value.
    public static IReadOnlyList<string> Build(ProcessTransportOptions options);
}
```

- [ ] **Step 1: Escrever os testes que falham**

```csharp
[Fact] public void The_fixed_arguments_come_first_in_the_checked_order()
// Build(new("C:\\demo\\app")) é exatamente:
// -p --input-format stream-json --output-format stream-json --verbose --include-partial-messages
// --permission-prompt-tool stdio --permission-mode default

[Fact] public void Resume_model_and_effort_follow_when_asked()
// ... --permission-mode plan --resume 8c1f6a52-0000-4000-8000-000000000001 --model claude-opus-5-5[1m] --effort high

[Theory] // modo: bypassPermissions, dontAsk, "", "plan --x"; modelo: "sonnet & calc", "-m", "a b", 101 caracteres;
         // sessão: "a\" --dangerously-skip-permissions", "../x", ""; effort: "ultra", "high low"
public void Values_that_could_become_another_command_are_refused(string field, string value)

[Fact] public void Models_the_claude_takes_are_accepted()
// sonnet, opus[1m], claude-opus-4-6, arn:aws:bedrock:us-east-1:123:inference-profile/x, claude-sonnet-5@20250514

[Fact] public void No_argument_outside_the_list_is_ever_built()
// para qualquer combinação válida, todo argumento que começa com "--" está na lista das Global Constraints

[Fact] public void A_null_or_empty_working_directory_is_refused()
```

- [ ] **Step 2: Rodar e ver falhar.** — FAIL de compilação, `ClaudeArguments` não existe.
- [ ] **Step 3: Escrever os dois arquivos.**
- [ ] **Step 4: Rodar e ver passar** nos dois targets.
- [ ] **Step 5: Commit** — `feat(dotnet): opções do ProcessTransport e a linha de comando do claude, conferida`

---

### Task 3: Achar o `claude`

**Files:**
- Create: `libs/dotnet/src/ClaudeCliProtocol/ClaudeLocator.cs`
- Create: `libs/dotnet/tests/ClaudeCliProtocol.Tests/ClaudeLocatorTests.cs`

**Interfaces:**
- Produces:

```csharp
public static class ClaudeLocator
{
    /// The full path of `claude` on the PATH, or null. On Windows it tries the extensions of PATHEXT
    /// (claude.exe before claude.cmd); elsewhere, the bare name when the file is executable.
    public static string? Find();
    internal static string? Find(string? path, string? pathExt, bool windows, Func<string, bool> isFile);
}
```

A pasta atual nunca é procurada: só as pastas do `PATH`.

- [ ] **Step 1: Escrever os testes que falham** (com a sobrecarga interna, sem tocar no `PATH` de verdade)

```csharp
[Fact] public void On_windows_the_first_folder_of_the_path_that_has_it_wins()
[Fact] public void On_windows_the_exe_comes_before_the_cmd_in_the_same_folder()
[Fact] public void Elsewhere_the_bare_name_is_looked_up()
[Fact] public void Nothing_found_gives_null()
[Fact] public void The_current_folder_is_not_searched()   // PATH com entrada vazia e com "." não acha um claude da pasta atual
```

- [ ] **Step 2 a 4:** ver falhar, escrever, ver passar.
- [ ] **Step 5: Commit** — `feat(dotnet): ClaudeLocator, acha o claude no PATH sem olhar a pasta atual`

---

### Task 4: O `ProcessTransport`: subir, ler, encerrar

**Files:**
- Create: `libs/dotnet/src/ClaudeCliProtocol/ProcessTransport.cs`
- Create: `libs/dotnet/tests/ClaudeCliProtocol.Tests/ProcessTransportTests.cs`

**Interfaces:**
- Consumes: `ProcessTransportOptions`, `ClaudeArguments.Build`, `ClaudeLocator.Find`, `IClaudeTransport`, `TransportState`; `FakeClaudeHost` (Tarefa 1).
- Produces:

```csharp
public sealed class ProcessTransport : IClaudeTransport
{
    public ProcessTransport(ProcessTransportOptions options);

    /// Starts the claude. FileNotFoundException when there is no claude; DirectoryNotFoundException
    /// when the working directory does not exist. Call it once.
    public void Start();

    /// The exit code, once the process has ended; null before.
    public int? ExitCode { get; }
    /// How many lines the claude wrote to stderr. The lines themselves are read and dropped.
    public int StderrLines { get; }

    public Task<string?> ReceiveLineAsync(CancellationToken ct);
    public Task SendAsync(ClaudeCommand command, CancellationToken ct);   // Tarefa 5
    public event EventHandler<TransportState>? StateChanged;
    public ValueTask DisposeAsync();   // ends the process and its children, then waits for the end
}
```

Regras:

| O que acontece | Efeito |
| --- | --- |
| `Start` | sobe o processo na pasta das opções, com o ambiente das Global Constraints, os três pipes e sem janela |
| uma linha no stdout | sai no `ReceiveLineAsync`, sem o `\r` do fim; linha em branco é pulada |
| o stdout acaba **e** o processo termina | o `ReceiveLineAsync` devolve `null`; `ExitCode` tem o código; `StateChanged` com `Closed`, uma vez |
| `ReceiveLineAsync` antes do `Start` | `InvalidOperationException` |
| `DisposeAsync` | fecha o stdin, espera até 2 segundos o processo sair sozinho, e senão mata a árvore (`Kill(entireProcessTree: true)` no `net6.0`; `taskkill /T /F /PID` no Windows do `netstandard2.0`, `Kill()` nos outros) |

- [ ] **Step 1: Escrever os testes que falham**

```csharp
[Fact] public async Task It_starts_the_claude_with_the_checked_arguments_in_the_given_folder()
// a linha fake_start: args == ClaudeArguments.Build(options); cwd == a pasta; NoDefaultCurrentDirectoryInExePath == "1"
[Fact] public async Task The_environment_of_the_options_is_added_and_a_null_value_removes()
[Fact] public async Task It_gives_the_lines_of_stdout_in_order_and_then_null()
[Fact] public async Task A_process_that_exits_at_once_closes_the_transport_with_its_exit_code()   // FAKE_CLAUDE_EXIT_NOW=3
[Fact] public async Task A_flood_on_stderr_does_not_block_the_process()   // 20.000 linhas no stderr; o roteiro chega inteiro; StderrLines == 20000
[Fact] public async Task Lines_are_read_as_utf8()                          // roteiro com "ação 日本"
[Fact] public async Task Dispose_ends_the_process()                        // o processo e o filho (FAKE_CLAUDE_CHILD=1) não existem mais
[Fact] public async Task A_cancelled_read_stops_and_the_next_read_still_works()
[Fact] public void A_missing_claude_or_folder_says_so()                    // FileNotFoundException; DirectoryNotFoundException
[Fact] public async Task It_reports_open_then_closed()
```

Todos sobem o `FakeClaude` com `SendInitialize = false`.

- [ ] **Step 2 a 4:** ver falhar, escrever, ver passar nos dois targets (o `net48` exercita o caminho do `taskkill`).
- [ ] **Step 5: Commit** — `feat(dotnet): ProcessTransport sobe o claude, lê o stdout, descarta o stderr e encerra a árvore`

---

### Task 5: O `ProcessTransport`: escrever e cumprir a regra

**Files:**
- Modify: `libs/dotnet/src/ClaudeCliProtocol/ProcessTransport.cs`
- Create: `libs/dotnet/tests/ClaudeCliProtocol.Tests/ProcessTransportSendTests.cs`

**Interfaces:**
- Consumes: `StdinWriter`, `PermissionLedger`, `StreamJsonParser`.
- Regras do `SendAsync` e do que o transporte faz sozinho:

| O que acontece | Efeito |
| --- | --- |
| `Start` com `SendInitialize` | escreve um `initialize` com id próprio antes de qualquer outra linha |
| cada linha lida do stdout | passa por um `StreamJsonParser` do transporte, e os eventos vão para o `PermissionLedger` dele |
| um `control_request` que não é `can_use_tool` (hook, MCP, elicitation) | o transporte responde `{"type":"control_response","response":{"subtype":"error","request_id":"…","error":"not supported"}}`, senão o `claude` espera para sempre; a linha continua saindo no `ReceiveLineAsync` |
| `SendAsync(SendUserMessage)` | `StdinWriter.Write(message)` |
| `SendAsync(ControlCommand)` | com o `RequestId` do comando, ou um novo quando é nulo |
| `SendAsync(AnswerPermission)` | `TryTake` no livro do transporte; achou, escreve `StdinWriter.Write(answer, request)`; não achou, não escreve nada |
| `SendAsync` depois de o processo terminar | não faz nada e não levanta exceção |
| duas escritas ao mesmo tempo | uma de cada vez, linha inteira |

- [ ] **Step 1: Escrever os testes que falham** (o `FakeClaude` devolve cada linha do stdin como `fake_stdin`, e é por ela que o teste vê o que foi escrito)

```csharp
[Fact] public async Task Initialize_goes_first()
[Fact] public async Task A_user_message_is_written_as_the_writer_writes_it()
[Fact] public async Task A_control_command_keeps_its_request_id_and_gets_one_when_it_has_none()
[Fact] public async Task The_answer_carries_the_input_this_transport_read()
// roteiro: a linha can_use_tool gravada na fixture "edit"; resposta Allow → updatedInput igual ao input da linha
[Fact] public async Task A_second_answer_writes_nothing()
[Fact] public async Task An_answer_to_a_cancelled_request_writes_nothing()
[Fact] public async Task An_answer_to_a_request_never_seen_writes_nothing()
[Fact] public async Task A_control_request_nobody_serves_is_answered_with_an_error()
[Fact] public async Task Every_line_written_is_ascii_and_ends_in_a_line_feed()   // mensagem "ação 日本" volta igual depois de lida como JSON
[Fact] public async Task Sending_after_the_process_ended_does_nothing()
[Fact] public async Task Writes_from_two_threads_do_not_mix()              // 200 mensagens de duas threads: 200 linhas inteiras
[Fact] public async Task A_session_over_the_transport_answers_a_permission_request()
// ClaudeSession + ProcessTransport + FakeClaude: o pedido aparece em PendingPermissions, a resposta chega ao stdin
```

- [ ] **Step 2 a 4:** ver falhar, escrever, ver passar nos dois targets.
- [ ] **Step 5: Commit** — `feat(dotnet): ProcessTransport escreve os comandos e responde permissão com o pedido que leu`

---

### Task 6: A `ClaudeSession` sobre um transporte que cai

Os três pontos que a revisão da fase 1 deixou adiados passam a importar com um processo de verdade.

**Files:**
- Modify: `libs/dotnet/src/ClaudeCliProtocol/ClaudeSession.cs`
- Modify: `libs/dotnet/tests/ClaudeCliProtocol.Tests/ClaudeSessionTests.cs`, `libs/dotnet/tests/ClaudeCliProtocol.Tests/ScriptedTransport.cs`

- [ ] **Step 1: Escrever os testes que falham**

```csharp
[Fact] public async Task A_read_that_throws_leaves_the_session_closed_and_idle()
// o transporte levanta IOException na leitura: a exceção sobe; depois, IsBusy false, nada pendente, TransportState Closed
[Fact] public async Task A_message_after_the_transport_closed_does_not_make_the_session_busy()
// SendAsync(SendUserMessage) depois do fim devolve false e não chama o transporte
[Fact] public async Task A_subscriber_that_throws_does_not_stop_the_send()
// StateChanged levanta exceção: o comando chega ao transporte mesmo assim
```

- [ ] **Step 2 a 4:** ver falhar, corrigir, ver passar (a suíte inteira).
- [ ] **Step 5: Commit** — `fix(dotnet): a ClaudeSession sobre um transporte que cai ou que já fechou`

---

### Task 7: O exemplo de console e o teste com o `claude` de verdade

**Files:**
- Create: `examples/dotnet-console/ClaudeConsole.csproj`, `examples/dotnet-console/Program.cs`, `examples/dotnet-console/README.md`
- Modify: `libs/dotnet/ClaudeCliProtocol.sln`, `examples/README.md`
- Create: `libs/dotnet/tests/ClaudeCliProtocol.Tests/LiveClaudeTests.cs`

**Interfaces:**
- O exemplo: `dotnet run --project examples/dotnet-console -- [pasta]`. Lê uma linha, manda como mensagem, escreve o texto da resposta conforme chega, mostra cada chamada de ferramenta, pergunta `[y/a/N]` em cada pedido de permissão e sai com uma linha vazia. `net6.0`, referência de projeto para a lib.
- `LiveClaudeTests`: um teste, `A_real_claude_answers_one_message`, que só roda com `CCP_LIVE_CLAUDE=1` (senão sai no começo, sem falhar). Sobe o `claude` do `PATH` numa pasta temporária vazia, em modo `plan`, manda "Reply with the single word: ready", e espera um `SessionStarted`, texto do assistente e um `TurnCompleted` sem erro, em até 120 segundos. Nega qualquer pedido de permissão.

- [ ] **Step 1: Escrever o teste ao vivo e ver que ele sai sem rodar** sem a variável.
- [ ] **Step 2: Escrever o exemplo.** `dotnet build libs/dotnet -c Release` compila o exemplo junto.
- [ ] **Step 3: Rodar o teste ao vivo uma vez**, com o ok do dono do projeto (usa a conta do `claude` desta máquina): `CCP_LIVE_CLAUDE=1 dotnet test libs/dotnet -c Release --framework net6.0 --filter "FullyQualifiedName~LiveClaudeTests"`. Registrar no README do exemplo a data, a versão do `claude` e o resultado. Sem o ok, o README diz que o exemplo compila e que não foi rodado contra um `claude`.
- [ ] **Step 4: Commit** — `feat(examples): exemplo de console em .NET e teste opcional com o claude de verdade`

---

### Task 8: O gravador

**Files:**
- Create: `tools/gravar.py`, `tools/cenarios.json`, `tools/test_gravar.py` (cópias de `<refs>/claude-visual-chat/tools/`, commit `16809aa`)
- Modify: `.github/workflows/ci.yml`, `.github/workflows/fixtures.yml`
- Modify: `fixtures/README.md`, `CONTRIBUTING.md`, `CONTRIBUTING.en.md`, `CONTRIBUTING.es.md`

**Interfaces:**
- Mudanças em relação à origem, e só estas: a saída padrão passa de `tests/fixtures` para `fixtures`; o texto de uso acompanha; em `test_gravar.py`, o dado de teste que cita uma pessoa pelo nome vira um texto sem nome.
- `python -I tools/gravar.py <cenário>... | --todos [--saida fixtures] [--claude <caminho>]`.

- [ ] **Step 1: Copiar `test_gravar.py` e ver falhar** — `python -I tools/test_gravar.py`: FAIL, `gravar` não existe.
- [ ] **Step 2: Copiar `gravar.py` e `cenarios.json`, aplicar as mudanças, ver passar.**
- [ ] **Step 3: Conferir o diff contra a origem** com `git diff --no-index`: só as mudanças listadas.
- [ ] **Step 4: `ci.yml`** — o job `repo` roda `python -I tools/test_gravar.py`. **`fixtures.yml`** — depois de gravar, instala o .NET, compila e roda os testes de conformidade com `CCP_UPDATE_EXPECTED=1`, para o pull request levar as gravações e os resultados esperados juntos; o corpo do pull request diz a versão do `claude`. Atualizar o teste `Workflows` de `tools/test_repo.py` para exigir os dois.
- [ ] **Step 5: Documentar** em `fixtures/README.md` e nos três `CONTRIBUTING` como gravar um cenário e o que o gravador troca. Dizer que a regravação semanal nunca rodou e que depende do secret.
- [ ] **Step 6: Commit** — `feat(tools): o gravador das fixtures, do CVC 16809aa, e a regravação com os resultados esperados`

---

### Task 9: Fechar a documentação

**Files:**
- Modify: `libs/dotnet/README.md`, `CHANGELOG.md`, `SECURITY.md`, `SECURITY.en.md`, `SECURITY.es.md`, os cinco `README*.md`, `site/src/content/docs/**/overview.md` e `index.mdx`, `docs/ROADMAP.md`, `docs/specs/2026-11-22-claude-cli-protocol-design.md`, `RELEASING.md`
- Modify: `libs/dotnet/tests/ClaudeCliProtocol.Tests/ReadmeExamplesTests.cs`, `libs/dotnet/tests/ClaudeCliProtocol.Tests/PublicApiTests.cs`

- [ ] **Step 1:** o exemplo de uso do `ProcessTransport` no README do pacote, copiado num teste que compila (e roda contra o `FakeClaude`).
- [ ] **Step 2:** `PublicApiTests`: `ProcessTransport`, `ProcessTransportOptions`, `ClaudeArguments` e `ClaudeLocator` são públicos.
- [ ] **Step 3:** o estado da fase 2 nos READMEs e no site; `SECURITY` sem a frase "parte do que está acima ainda não foi escrita"; o README do pacote diz onde a regra passa a ser cumprida; `CHANGELOG`; os itens 1, 2 e 7 do ROADMAP viram ✅ e os que dependem de um `claude` de verdade ficam como estão; a spec ganha as decisões desta fase.
- [ ] **Step 4:** rodar tudo: build, testes nos dois targets, pacote, `tools/test_repo.py`, `tools/test_gravar.py`, `docs/test_sweep_issues.py`, site.
- [ ] **Step 5: Commit** — `docs: estado da fase 2, o ProcessTransport no README do pacote e a regra onde ela se cumpre`

---

### Task 10: Revisão, pull request e CI

- [ ] **Step 1:** revisão do branch inteiro por um revisor de contexto novo; corrigir o que for crítico ou importante, cada correção com um teste que falha antes.
- [ ] **Step 2:** `git push -u origin fase-2-processo` e pull request contra o `master`. O CI roda o `FakeClaude` no Windows e no Linux pela primeira vez.
- [ ] **Step 3:** parar e mostrar ao dono do projeto: o que rodou, o que não rodou (a regravação semanal, e o teste ao vivo se não houve ok) e o que falta para publicar no NuGet.
