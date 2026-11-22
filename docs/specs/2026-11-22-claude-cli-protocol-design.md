# claude-cli-protocol: o protocolo do `claude -p` em lib própria, com transporte trocável

Data: 2026-11-22.

**Origem.** Esta spec vem de um desenho maior, do dono do projeto, com três camadas: a lib do
protocolo, o componente de chat e o aplicativo que os integra. Ela fica só com o que é deste
repositório, a camada de baixo. O que é das outras camadas aparece aqui só como referência.

**Conferido no código em 2026-11-22**, em clones rasos:

| Repositório | Commit | O que foi lido |
| --- | --- | --- |
| `carlosdealmeida/claude-todos-vscode` | `5e84db3` | o padrão: READMEs, SECURITY, CONTRIBUTING, RELEASING, CHANGELOG, templates, workflows, `site/`, `docs/` |
| `carlosdealmeida/claude-visual-chat` | `16809aa` | `Protocol/`, os testes, as fixtures, `tools/gravar.py`, `Directory.Build.props` |
| o supervisor de referência (privado) | `aa8d30d` | o módulo que sobe o `claude -p` e fala com ele |

## Por quê

- **O protocolo mora dentro do componente.** O `ClaudeVisualChat.Avalonia` 0.10.0 traz a pasta
  `Protocol/` (3.152 linhas em 10 arquivos). Quem quer só o protocolo leva o Avalonia, o
  `Markdown.Avalonia.Tight` e o SkiaSharp junto, e uma lib em outra linguagem não tem de onde partir.
- **O protocolo está escrito três vezes:** no CVC (C#), num supervisor em Python que sobe o
  `claude -p` em máquinas remotas (privado, do dono do projeto; aqui, "o supervisor de referência")
  e no aplicativo que usa os dois. Nenhuma das três tem uma especificação comum para conferir.
- **Não existe documentação unificada do `claude -p`.** O que se sabe do stream-json está espalhado
  em código, testes e gravações. Este repositório junta a especificação escrita, as gravações que a
  provam e as libs que a implementam.

## As camadas

```
claude-cli-protocol (lib)      ← só fala o protocolo; não sabe que tela existe
   ↑
ClaudeVisualChat.Avalonia      ← só desenha; não abre processo nem rede
   ↑
o aplicativo                   ← só integra
```

Cada camada só conhece a de baixo. A dependência é **por pacote com versão** (NuGet, no .NET), nunca
por referência de projeto nem por código copiado. Este repositório é a camada de baixo: não conhece
o aplicativo, a rede por onde ele fala nem o Avalonia.

## O repositório

Público, MIT. Projeto da comunidade, sem vínculo com a Anthropic; os READMEs e a descrição do
pacote dizem isso, como no CVC ("Community project, not affiliated with or endorsed by Anthropic").

```
claude-cli-protocol/
  site/          → landing (Astro) e especificação (Starlight), nos cinco idiomas
  docs/          → specs, planos, ROADMAP e glossário (docs/i18n)
  fixtures/      → as gravações e o resultado esperado de cada uma
  libs/dotnet/   → ClaudeCliProtocol (NuGet)
  libs/delphi/   → a lib em Delphi, piso 10.1 Berlin (fase 6)
  examples/      → console .NET, e depois o exemplo de console em Delphi
  tools/         → o gravador das fixtures (fase 2)
```

Tela nenhuma entra aqui, em linguagem nenhuma. O componente Avalonia continua no
`claude-visual-chat`; o componente VCL vai para um repositório próprio.

**Idioma.** O código, os nomes da API e os comentários ficam em inglês. A documentação tem o
português como padrão e as traduções ao lado (ver "O padrão do repositório"). O desenho original dizia
"em inglês como o CVC" num ponto e "cinco idiomas, com o português como padrão" em outro; vale o
segundo, confirmado pelo dono do projeto em 2026-11-22.

## A lib .NET

### Build

- `TargetFrameworks`: `netstandard2.0;net6.0`. O `netstandard2.0` é o piso enquanto houver
  aplicativo consumidor rodando em Windows 7 x86 e o CVC mirar `netstandard2.0`. Trocar o piso só
  por decisão explícita numa spec.
- `LangVersion` 10, `Nullable` ligado, `ImplicitUsings` desligado e `TreatWarningsAsErrors`, como
  no `Directory.Build.props` do CVC.
- Dependências: `System.Text.Json` 8.0.5 (a mesma do CVC) e, só no `netstandard2.0`,
  `Microsoft.Bcl.AsyncInterfaces` (por `IAsyncDisposable`). Nada de Avalonia, de SkiaSharp nem de
  qualquer pacote de tela.
- Pacote `ClaudeCliProtocol` (nome provisório; livre no nuget.org em 2026-11-22), com símbolos
  (`snupkg`) e o README dentro, como o CVC.
- Namespace `ClaudeCliProtocol`.

### O que a lib tem

- **Mensagens:** os tipos do stream-json (`system`, `assistant`, `user`, `result`, `stream_event`,
  `control_request`, `control_response`, `rate_limit_event` e os blocos de conteúdo), tirados do
  `Protocol/` do CVC.
- **Leitura:** `StreamJsonParser` (ao vivo) e `TranscriptParser` (o `.jsonl` da sessão em disco),
  também do CVC. Os dois nunca levantam exceção: a linha que não entendem não gera evento.
- **Comandos:** `ClaudeCommand`, um tipo por coisa que se manda ao `claude`: a mensagem do usuário
  (com imagens), a resposta de permissão, o modo, o modelo, o effort, o interrupt e o `initialize`.
  O `StdinMessages` de hoje também escreve `get_settings`, `get_context_usage`, `get_task_output`,
  `remote_control` e `get_chrome_dialog`; eles vêm junto. Os comandos recebem só dados. Hoje o
  `StdinMessages` recebe os `EventArgs` da tela (`MessageSubmittedEventArgs`,
  `PermissionAnsweredEventArgs`); essa conversão fica no CVC.
- **Sessão:** `ClaudeSession`, que junta um transporte a um parser e devolve eventos tipados. Ela
  casa cada `control_request` com a resposta pelo `request_id` e acompanha o estado: ocupada,
  permissões pendentes, `session_id`, modo e modelo confirmados.

### O que vem do CVC

A pasta `src/ClaudeVisualChat.Avalonia/Protocol/`, no commit `16809aa`:

| Arquivo | Linhas | O que é |
| --- | ---: | --- |
| `ClaudeEvents.cs` | 321 | os eventos (`ClaudeEvent` e os 36 records que o estendem) |
| `ContentBlocks.cs` | 219 | os blocos de conteúdo e os leitores de JSON internos |
| `StreamJsonParser.cs` | 299 | a leitura ao vivo |
| `TranscriptParser.cs` | 743 | a leitura do arquivo da sessão e o `EventReader` interno |
| `StdinMessages.cs` | 211 | as linhas do stdin |
| `TaggedMessage.cs` | 693 | o texto do usuário que é um bloco com marcação: mensagem de um agente, de outra sessão ou de um canal, aviso de tarefa em segundo plano |
| `LightMarkup.cs` | 324 | o leitor de marcação que o `TaggedMessage` usa |
| `ContextBlocks.cs` | 244 | o contexto que a IDE manda com o prompt |
| `AttachedFileLine.cs` | 59 | a linha `[attached file: <caminho>]` |
| `AnsiText.cs` | 39 | tira os códigos de terminal |

Os testes que vêm junto: `Protocol/StreamJsonParserTests.cs`, `Protocol/TranscriptParserTests.cs`,
`StdinMessagesTests.cs`, `TaggedMessageTests.cs`, `ContextBlockReaderTests.cs`, `AnsiTextTests.cs`,
`FixturePrivacyTests.cs` e `FixtureFiles.cs`.

**A pasta não se sustenta sozinha.** O `Protocol/` usa tipos que hoje estão fora dele, no namespace
`ClaudeVisualChat`:

| Tipo | Onde está no CVC | O que acontece na lib |
| --- | --- | --- |
| `ChatCommand`, `ChatModel`, `ContextCategory`, `ContextFile`, `ContextAgent`, `ContextUsage`, `UsageWindow`, `UsageLimits`, `AccountInfo`, `AuthConflict` | `ChatCatalog.cs` | vêm inteiros: são só dados, e os eventos os carregam. `ChatCommand` vira `SlashCommand` e `ChatModel` vira `ModelOption` |
| `ChatImage` | `ChatImage.cs` | a lib ganha um tipo só de dados (`MediaType` e `Data` em base64). A largura e a altura dependem do SkiaSharp (`ImageFitter.Measure`) e ficam no CVC |
| `MessageSubmittedEventArgs`, `PermissionAnsweredEventArgs`, `PendingPermission`, `ChatFile`, `PermissionDecision` | `ChatEvents.cs`, `PendingPermission.cs`, `ChatFile.cs` | não vêm: são da tela. Os comandos da lib recebem os dados que eles carregam. A decisão (`allow`, `allow always`, `deny`) vira um enum da lib |
| `IsExternalInit` | `IsExternalInit.cs` | a lib tem o seu, para os records compilarem no `netstandard2.0` |

No sentido contrário, o CVC usa hoje seis coisas internas do `Protocol/`: `JsonHelpers`,
`ContextBlock`, `TaggedMessage`, `TaggedSection`, `TaskNotificationText` e `UsageLimits.IsSnapshot`.
Com a lib em outro assembly, cada uma precisa virar API pública ou ficar copiada no CVC. O que vira
público está em "Decidido em 2026-11-22", item 3.

## Os transportes

A interface é de comandos, não de stdin cru. Quem está do lado do processo é quem escreve o comando
no stdin:

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

- **`ReplayTransport`** (fase 1): lê uma gravação e ignora os comandos. Serve aos testes, à
  conformidade e ao sample do CVC.
- **`ProcessTransport`** (fase 2): sobe o `claude` localmente e escreve o comando no stdin.
- **Um transporte por rede**: não é daqui. Fica no aplicativo que fala com um `claude` remoto por
  uma rede própria. Aparece nesta spec só para mostrar que a interface aguenta um transporte que
  cai e reconecta.

### A regra de segurança da resposta de permissão

Vem do supervisor de referência e vale para todo transporte que escreve no stdin de um `claude`:

> A resposta a um pedido de permissão leva sempre o `input` e as `permission_suggestions` que o
> próprio lado do processo recebeu do `claude`. Quem chama só escolhe o `request_id`, a decisão e a
> mensagem.

Por isso o comando de resposta de permissão não tem campo para o `input` nem para as sugestões. O
lado do processo guarda cada `can_use_tool` que chega e monta a resposta com o que guardou:

- `deny`: `{"behavior": "deny", "message": <mensagem>}`. Sem mensagem, vai
  `"The user denied this tool use."`.
- `allow`: `{"behavior": "allow", "updatedInput": <o input guardado>}`.
- `allow always`: o mesmo, mais `"updatedPermissions": <as sugestões guardadas>`, quando elas são
  uma lista.
- Um `request_id` que não está pendente (já respondido, cancelado, resolvido ou desconhecido) não
  escreve nada. Responder tira o pedido da lista, o que impede a segunda resposta.

### O que o `ProcessTransport` espelha do supervisor de referência (para a fase 2)

Conferido no código dele, commit `aa8d30d`:

- **Argumentos:** `-p --input-format stream-json --output-format stream-json --verbose
  --include-partial-messages --permission-prompt-tool stdio --permission-mode <modo>`. Opcionais:
  `--resume <id>`, `--model <modelo>`, `--effort <nível>`. Nunca `--setting-sources`: as regras de
  bloqueio de cada máquina valem em qualquer modo.
- **Opções do transporte:** o caminho do CLI, a pasta, o modo e as variáveis de ambiente. Entre elas
  está o `CLAUDE_CONFIG_DIR`, que escolhe a conta.
- **Conferência do que vai à linha de comando.** No Windows, o `claude.cmd` do npm passa pelo
  `cmd.exe`. O supervisor aceita só os modos `plan`, `default`, `acceptEdits` e `auto`, e recusa modelo
  e id de sessão fora de um padrão fechado de caracteres.
- **Ambiente:** `NoDefaultCurrentDirectoryInExePath=1`, para o `cmd.exe` não procurar o `node` na
  pasta da conversa.
- **stdin:** uma linha JSON por mensagem, só ASCII (o resto escapado), o que chega igual ao `node`
  qualquer que seja a codificação do pipe. O `StdinMessages` do CVC já escreve assim.
- **stdout:** UTF-8, linha a linha. **stderr:** lido e descartado, porque pode ter texto da
  conversa; ler é preciso, senão o pipe enche e o `claude` trava.
- **Partida:** o `initialize` primeiro.
- **Pedido de controle que ninguém atende** (hook, MCP, elicitation): responde com erro, senão o
  `claude` espera para sempre.
- **Fim:** vem do processo, não do EOF do stdout. Encerrar mata a árvore de processos.

## As fixtures e a conformidade

**A conformidade é das fixtures.** Toda lib, em qualquer linguagem, passa pela mesma suíte: lê a
gravação e produz o resultado esperado.

- **O que existe hoje** (CVC, `tests/fixtures/`): 21 cenários, cada um com dois arquivos,
  `<cenário>.stream.ndjson` (a saída do `claude -p`) e `<cenário>.transcript.jsonl` (o arquivo da
  sessão). São 42 arquivos e 1.072.314 bytes. Os cenários: `edit`, `bash`, `subagent`, `error`,
  `denied`, `interrupted`, `mode-and-model`, `image`, `commands`, `effort`, `plan`, `plan-auto`,
  `plan-feedback`, `queued`, `hook-blocked`, `queued-compact`, `error-long`, `error-wrapped`,
  `denied-long`, `hook-ask` e `seguranca`.
- **O resultado esperado:** um arquivo por gravação, em JSON neutro: a lista de eventos, cada um com
  o tipo e os campos que importam. Neutro quer dizer sem nada de .NET: nomes de campo e valores que
  uma lib em Delphi produz igual. O formato é decidido na fase 1, junto com os primeiros testes.
- **A gravação:** `tools/gravar.py` e `tools/cenarios.json` do CVC sobem o `claude -p` de verdade,
  rodam o roteiro de cada cenário e higienizam o resultado (pasta, usuário e home trocados pelos de
  `demo`; o `system/init` reduzido a campos conhecidos). Vêm para cá na fase 2.
- **A regravação semanal:** o `fixtures.yml` roda o gravador com a API key e abre um PR quando
  alguma gravação muda. Quando o CLI muda, o CI quebra antes de alguém descobrir em uso.
- **Privacidade:** o teste `FixturePrivacyTests` vem junto. Nenhuma gravação entra com nome de
  usuário, pasta, e-mail ou credencial de quem gravou.
- **Segredo:** a API key fica só como secret do GitHub (`ANTHROPIC_API_KEY`), referenciada no
  `fixtures.yml`. Nenhum segredo entra no repositório.

## O padrão do repositório

O repositório nasce no nível do `claude-todos-vscode`, desde a fase 0.

- **Cinco idiomas, com o português como padrão:** `README.md` em pt-BR e `README.en.md`,
  `README.es.md`, `README.zh-cn.md` e `README.zh-tw.md`, com a linha de troca de idioma no topo. O
  glossário de termos fica em `docs/i18n/`. As traduções para o chinês são feitas por IA e dizem
  isso, como lá.
- **Segurança:** `SECURITY.md` (pt-BR, en, es), com relato privado pelo GitHub (Security → Report a
  vulnerability), os mesmos prazos de resposta e o escopo desta lib: o processo que ela sobe, os
  argumentos e as variáveis de ambiente que passa, a regra da resposta de permissão e o que ela
  nunca grava em disco.
- **Contribuição e release:** `CONTRIBUTING.md` (pt-BR, en, es), `RELEASING.md`, `CHANGELOG.md`
  (Keep a Changelog) e `LICENSE` (MIT); `.github/ISSUE_TEMPLATE` com bug, pedido e `config.yml`.
- **`.editorconfig` e `.gitattributes`:** LF em tudo, como nos dois repositórios de referência.
- **CI no GitHub Actions:**
  - `ci.yml`: build e testes da lib .NET nos dois targets, da lib Delphi quando existir, e o build
    do site;
  - `release.yml`: a tag `v<semver>` confere a versão, testa, empacota (NuGet; Delphi como zip) e
    cria o GitHub Release com o `CHANGELOG`;
  - `pages.yml`: o site;
  - `fixtures.yml`: a regravação semanal das fixtures.
- **Vigia de quebra:** além das fixtures, uma varredura das issues e do CHANGELOG do
  `anthropics/claude-code` que tocam o `-p` e o stream-json, no molde do `docs/sweep_issues.py` do
  `claude-todos`. O que mudou vai para o `docs/ROADMAP.md`.
- **Método:** `docs/specs/` e `docs/plans/`. As decisões do protocolo moram aqui. Commits pequenos,
  um por tarefa, com mensagem em português: `feat(<área>): …`, `docs(<área>): …`, `chore: …`.

## O site

GitHub Pages, em **Astro + Starlight**. O Astro é o mesmo do site do `claude-todos`, e a landing é
feita nele. A especificação usa o Starlight, o tema de documentação do próprio Astro, que traz
busca, navegação e i18n prontos, nos cinco idiomas. Componentes Svelte podem tocar uma fixture na
página e mostrar, evento por evento, o que o CLI manda.

Na fase 0 o site nasce com a estrutura, os cinco idiomas e o build passando. O conteúdo da
especificação (protocolo, comandos, controle, transporte, fixtures) é a fase 5.

## Delphi

- **Piso: Delphi 10.1 Berlin.** Tem `System.JSON`, `System.Threading` (`TTask`) e
  `System.Generics.Collections`. Não tem variável inline nem inferência de tipo (só a partir do
  10.3), então o código da lib não as usa.
- O processo sobe pela API do Windows (`CreateProcess` com pipes), sem pacote de terceiros.
- A lib passa nas mesmas fixtures da lib .NET.
- O componente VCL não entra aqui: vai para um repositório próprio, como o CVC.
- Até a fase 6, `libs/delphi/` tem só um README que diz isso.

## Cobrança e conta

Não são da lib. Ela só passa adiante o que a máquina já tem: o login da assinatura ou a
`ANTHROPIC_API_KEY`. A documentação recomenda API key para quem embutir o chat num produto de
terceiros.

## Fases

| Fase | O que sai | Repositório |
| --- | --- | --- |
| 0 | O esqueleto no padrão: pastas, READMEs nos cinco idiomas, SECURITY, CONTRIBUTING, RELEASING, CHANGELOG, LICENSE, templates, os quatro workflows e o site com o build passando | claude-cli-protocol |
| 1 | O `Protocol/` do CVC em `libs/dotnet`, comandos como dados, `ClaudeSession`, `ReplayTransport`, testes contra as fixtures que já existem no CVC | claude-cli-protocol |
| 2 | `ProcessTransport` e o exemplo de console; o gravador e a regravação semanal das fixtures | claude-cli-protocol |
| 3 | CVC 0.11 sobre a lib | claude-visual-chat |
| 4 | O aplicativo sobre o CVC novo, com o transporte por rede dele (só referência) | repositório privado |
| 5 | A especificação no Starlight: protocolo, comandos, controle, transporte, fixtures | claude-cli-protocol |
| 6 | Lib em Delphi: processo com pipes, parser e eventos, passando nas mesmas fixtures | claude-cli-protocol |
| 7 | Componente de chat VCL sobre a lib Delphi | repositório próprio |

Cada fase tem plano próprio em `docs/plans/`. A fase 0 foi separada da fase 1 do desenho
original, para o padrão ficar de pé antes do código chegar.

## Pronto quando

O que é deste repositório, das fases 0 a 2:

1. O `ClaudeCliProtocol` sai no NuGet com os dois targets, sem Avalonia, e passa nas fixtures.
2. O CI roda os testes nos dois targets e o build do site a cada push.
3. Uma gravação que muda abre um PR sozinha.

Os itens do CVC (0.11 passando nos testes de hoje) e do aplicativo ficam com os repositórios
deles.

## Fora do escopo

- Hooks e tools customizadas em processo (o MCP dentro do app). Entram como spec própria depois da
  fase 2, se houver uso.
- Mudar o supervisor de referência. Ele continua sendo o processo do lado remoto; a lib .NET só
  o espelha.
- Fechar o código. A spec e as libs são abertas, como o CVC.
- Qualquer tela.

## A decidir

Do desenho original:

- O nome do pacote. `ClaudeCliProtocol` está provisório.
- O nome do repositório do componente VCL.

## Decidido em 2026-11-22

Achados na leitura do código, com a resposta do dono do projeto a cada um.

1. **`[TypeForwardedTo]` e a troca de namespace não andam juntos (fase 3).** O desenho original
   pedia as duas coisas. O `[TypeForwardedTo]` só encaminha um tipo com o mesmo nome completo:
   `ClaudeVisualChat.Protocol.StreamJsonParser` não pode ser encaminhado para
   `ClaudeCliProtocol.StreamJsonParser`. A lib usa o namespace `ClaudeCliProtocol`. O CVC 0.11
   mantém `ClaudeVisualChat.Protocol.StdinMessages`, que já precisa ficar lá por causa dos
   `EventArgs`, e quem usa os parsers ou os eventos direto troca um `using`. O detalhe é da spec da
   fase 3, no `claude-visual-chat`.
2. **Os nomes dos tipos de `ChatCatalog.cs`.** `ChatCommand` (um comando de barra que a sessão
   oferece) ficaria ao lado de `ClaudeCommand` (o que se manda ao transporte). Na lib, `ChatCommand`
   vira `SlashCommand` e `ChatModel` vira `ModelOption`; os outros mantêm o nome.
3. **O que do `Protocol/` vira API pública.** `TaggedMessage`, `TaggedSection` e
   `UsageLimits.IsSnapshot` ficam públicos, porque dizem algo do protocolo. `JsonHelpers`,
   `ContextBlock` e `TaskNotificationText` continuam internos, e o CVC resolve do lado dele na
   fase 3.
4. **Quem gera o `request_id` dos pedidos de controle.** Hoje é o app (`"req_" + ++ids`); no
   supervisor de referência, é a própria sessão. Na lib, quem chama não escolhe o id: quem manda o pedido o gera, e a
   `ClaudeSession` acompanha a resposta por ele.
5. **Como testar os dois targets.** Um projeto de teste não roda em `netstandard2.0`. Os testes
   miram `net6.0`, que usa a build `net6.0` da lib, e `net48` só no Windows, que usa a build
   `netstandard2.0`. O CI roda em `windows-latest` e `ubuntu-latest`.
6. **O idioma na raiz do site.** O português fica na raiz, como nos READMEs, e os outros quatro
   ganham prefixo (`/en/`, `/es/`, `/zh-cn/`, `/zh-tw/`). O site do `claude-todos` faz o contrário
   (inglês na raiz, português em `/pt/`).
7. **Branch padrão:** `master`, como no `claude-todos`.
8. **Escopo da fase 1:** a fase 1 inteira da tabela: mover o `Protocol/`, comandos como dados,
   `ClaudeSession`, `ReplayTransport` e os testes contra as fixtures.
