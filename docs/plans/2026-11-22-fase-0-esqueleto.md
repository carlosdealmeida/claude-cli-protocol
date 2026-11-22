# Fase 0: o esqueleto no padrão — Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Deixar o repositório no nível do `claude-todos-vscode` antes de o código chegar: pastas, documentação em cinco idiomas, uma solução .NET vazia que compila e testa nos dois targets, o site em Astro + Starlight com o build passando e os quatro workflows.

**Architecture:** Três árvores independentes. A documentação mora na raiz e em `docs/`, conferida por um teste de estrutura em Python (`tools/test_repo.py`). A lib .NET mora em `libs/dotnet/`, com solução própria. O site mora em `site/`, com `package.json` próprio. Os workflows só chamam os mesmos comandos que rodam na máquina.

**Tech Stack:** .NET SDK 8 (build) com targets `netstandard2.0;net6.0`, xunit 2.9.2; Astro 7 + Starlight 0.43; Python 3 (só biblioteca padrão, mais PyYAML opcional); GitHub Actions.

**Spec:** [docs/specs/2026-11-22-claude-cli-protocol-design.md](../specs/2026-11-22-claude-cli-protocol-design.md)

## Global Constraints

- **Referências:** clones rasos, numa pasta fora deste repositório (`<refs>`), de `claude-todos-vscode` em `5e84db3` e de `claude-visual-chat` em `16809aa`: `gh repo clone carlosdealmeida/<nome> -- --depth 1`. Nada dela entra neste repositório sem passar por uma tarefa.
- **Idioma:** código, nomes de arquivo de código, nomes da API e comentários em inglês. Documentação com o português como padrão e as traduções ao lado.
- **Cinco idiomas:** `pt-BR` (padrão, arquivo sem sufixo), `en`, `es`, `zh-cn`, `zh-tw`. SECURITY e CONTRIBUTING em três: `pt-BR`, `en`, `es`.
- **Datas em ISO** (`2026-11-22`).
- **Não afirmar o que não rodou.** Nenhum texto diz que o pacote está publicado, que o site está no ar ou que um workflow passou antes de isso ter acontecido.
- **Lib .NET:** `TargetFrameworks` `netstandard2.0;net6.0`, `LangVersion` 10.0, `Nullable` enable, `ImplicitUsings` disable, `TreatWarningsAsErrors` true, `System.Text.Json` 8.0.5 e, só no `netstandard2.0`, `Microsoft.Bcl.AsyncInterfaces` 8.0.0. Nada de Avalonia.
- **Testes .NET:** `net6.0` em qualquer sistema e `net48` só no Windows.
- **Branch:** `master`.
- **Nenhum segredo no repositório.** Os únicos secrets que um workflow pode citar são `ANTHROPIC_API_KEY` (só no `fixtures.yml`), `NUGET_USER` (só no `release.yml`) e `GITHUB_TOKEN`.
- **Aviso em todo README e na descrição do pacote:** projeto da comunidade, sem vínculo com a Anthropic.
- **Commits:** um por tarefa, mensagem em português, `feat(<área>): …`, `docs(<área>): …` ou `chore: …`. Nenhum `git push` neste plano antes da Tarefa 10.
- **Comandos:** rodar da raiz do repositório. Python sempre com `-I`.

## Review Focus

1. **Uma tradução que perde uma seção do README em português.** Quem lê em espanhol recebe menos que quem lê em português. O teste `test_all_readmes_have_the_same_sections` (Tarefa 4) compara a contagem de títulos `##` dos cinco.
2. **Um link relativo quebrado num arquivo traduzido.** O teste `test_relative_links_resolve` (Tarefa 1) abre cada link relativo de cada `.md` da raiz e de `docs/`, e roda de novo a cada tarefa.
3. **Um checkout no Windows que grava CRLF.** As fixtures da fase 1 são comparadas byte a byte. O teste `test_index_has_no_crlf` (Tarefa 1) lê `git ls-files --eol`.
4. **Um segredo colado num arquivo, ou um workflow que cita um secret que ninguém documentou.** Os testes `test_no_api_key_in_the_repo` e `test_workflows_only_name_known_secrets` (Tarefa 8).
5. **O site publicado numa subpasta.** No GitHub Pages ele fica em `/claude-cli-protocol/`, e um link que começa em `/` aponta para fora. O `site/scripts/check-dist.mjs` (Tarefa 7) confere os `href` internos das páginas geradas.

---

### Task 1: A base do repositório e o teste de estrutura

**Files:**
- Create: `.gitattributes`, `.editorconfig`, `.gitignore`, `LICENSE`
- Create: `tools/test_repo.py`

**Interfaces:**
- Produces: `tools/test_repo.py`, um módulo `unittest` que as tarefas 2 a 8 estendem com uma classe cada. Roda com `python -I tools/test_repo.py -v`. Expõe:
  - `ROOT` (a raiz do repositório, `pathlib.Path`), `read(rel)` (o texto UTF-8 de um arquivo) e `tracked()` (a lista de `git ls-files`);
  - `LANGUAGE_LABELS = ("Português", "English", "Español", "简体中文", "繁體中文")`, na ordem pt-BR, en, es, zh-cn, zh-tw;
  - `relative_links(text) -> list[str]`: o destino de cada link Markdown que não começa com `http`, `mailto:` ou `#`, sem a âncora. Ignora o que está em bloco de código cercado e entre crases, porque os planos trazem exemplos de link dentro de código.
- **Regra para as tarefas 2 a 5:** um documento só aponta com link para um arquivo que já existe. O que ainda vai chegar é citado em código (`` `release.yml` ``), e o link entra na tarefa que cria o arquivo. Assim toda tarefa termina com o teste de links verde.

- [ ] **Step 1: Escrever o teste que falha**

`tools/test_repo.py`, classe `BaseFiles`:

```python
def test_base_files_exist(self):
    for rel in (".gitattributes", ".editorconfig", ".gitignore", "LICENSE"):
        self.assertTrue((ROOT / rel).is_file(), rel)

def test_license_is_mit_and_names_the_author(self):
    text = read("LICENSE")
    self.assertIn("MIT License", text)
    self.assertIn("Copyright (c) 2026 carlosdealmeida", text)

def test_index_has_no_crlf(self):
    out = subprocess.run(["git", "ls-files", "--eol"], cwd=ROOT, capture_output=True, text=True, check=True).stdout
    bad = [line for line in out.splitlines() if line.startswith("i/crlf") or line.startswith("i/mixed")]
    self.assertEqual(bad, [])

def test_relative_links_resolve(self):
    for rel in [p for p in tracked() if p.endswith(".md") and ("/" not in p or p.startswith("docs/"))]:
        for link in relative_links(read(rel)):
            self.assertTrue((ROOT / rel).parent.joinpath(link).exists(), f"{rel} -> {link}")
```

O último já tem o que conferir: a spec e este plano, em `docs/`.

- [ ] **Step 2: Rodar e ver falhar**

Run: `python -I tools/test_repo.py -v`
Expected: FAIL em `test_base_files_exist` (`.gitattributes`).

- [ ] **Step 3: Criar os quatro arquivos**

- `.gitattributes`: `* text=auto eol=lf`; binários `*.png`, `*.gif`, `*.ico`, `*.nupkg`, `*.snupkg`; `*.svg text eol=lf`; e os arquivos do Delphi com CRLF, porque a IDE do 10.1 Berlin perde a linha do breakpoint com LF: `*.pas`, `*.dpr`, `*.dpk`, `*.dproj`, `*.dfm`, `*.inc` com `text eol=crlf`.
- `.editorconfig`: o do CVC (`utf-8`, espaços, 4, `lf`, linha final, sem espaço no fim) com `indent_size = 2` para `json,yml,yaml,csproj,props,md,mdx,astro,mjs,ts`; `[*.{pas,dpr,dpk,inc}]` com `end_of_line = crlf` e `indent_size = 2`.
- `.gitignore`: `bin/`, `obj/`, `*.user`, `.vs/`, `*.nupkg`, `*.snupkg`, `artifacts/`, `__pycache__/`, `.pytest_cache/`, `node_modules/`, `.DS_Store`, `docs/superpowers/`, `.superpowers/`, `.worktrees/`, `.claude/settings.local.json`, `.claude/worktrees/`, `.idea/`, `libs/dotnet/tests/private-words.txt`, `sweep_results.json` e, do Delphi, `*.dcu`, `__history/`, `__recovery/`, `*.local`, `*.identcache`.
- `LICENSE`: o texto MIT do `claude-todos-vscode`, com `Copyright (c) 2026 carlosdealmeida`.

- [ ] **Step 4: Rodar e ver passar**

Run: `git add -A && python -I tools/test_repo.py -v`
Expected: 4 testes, OK. (O `git add` vem antes porque os testes leem o índice do git.)

- [ ] **Step 5: Commit**

```bash
git commit -m "chore: base do repositório (.gitattributes, .editorconfig, .gitignore, LICENSE) e teste de estrutura"
```

---

### Task 2: RELEASING, CHANGELOG e os modelos de issue

**Files:**
- Create: `RELEASING.md`, `CHANGELOG.md`
- Create: `.github/ISSUE_TEMPLATE/bug_report.yml`, `.github/ISSUE_TEMPLATE/feature_request.yml`, `.github/ISSUE_TEMPLATE/config.yml`
- Modify: `tools/test_repo.py` (classe `ReleaseDocs`)

**Interfaces:**
- Produces: em `tools/test_repo.py`, `load_yaml(rel)`, que devolve o documento com PyYAML ou levanta `unittest.SkipTest("PyYAML not installed")` (a Tarefa 8 a reusa), e a constante `ADVISORIES` (a Tarefa 3 a reusa).

- [ ] **Step 1: Escrever os testes que falham**

```python
ADVISORIES = "https://github.com/carlosdealmeida/claude-cli-protocol/security/advisories/new"

def test_changelog_follows_keep_a_changelog_and_has_nothing_released(self):
    text = read("CHANGELOG.md")
    self.assertIn("https://keepachangelog.com/en/1.1.0/", text)
    self.assertIn("## [Unreleased]", text)
    self.assertNotRegex(text, r"## \[\d+\.\d+\.\d+\]")

def test_issue_templates(self):
    config = load_yaml(".github/ISSUE_TEMPLATE/config.yml")
    self.assertIs(config["blank_issues_enabled"], False)
    self.assertEqual(config["contact_links"][0]["url"], ADVISORIES)
    bug = load_yaml(".github/ISSUE_TEMPLATE/bug_report.yml")
    ids = [item.get("id") for item in bug["body"]]
    for wanted in ("description", "steps", "library", "lib-version", "claude-version", "os"):
        self.assertIn(wanted, ids)
    self.assertEqual(load_yaml(".github/ISSUE_TEMPLATE/feature_request.yml")["labels"], ["enhancement"])

def test_releasing_names_the_tag_and_the_only_secret_it_needs(self):
    text = read("RELEASING.md")
    self.assertIn("v<semver>", text)
    self.assertIn("NUGET_USER", text)
```

- [ ] **Step 2: Rodar e ver falhar**

Run: `python -I tools/test_repo.py ReleaseDocs -v`
Expected: FAIL (`CHANGELOG.md` não existe).

- [ ] **Step 3: Escrever os cinco arquivos**

- `CHANGELOG.md`: em inglês, como nos dois repositórios de referência (ele vira o corpo do GitHub Release e as notas do pacote). Cabeçalho do Keep a Changelog e `## [Unreleased]` com `### Added` e um item: "Repository skeleton: documentation in five languages, CI, the site and an empty .NET solution for both targets."
- `RELEASING.md`: em português. O que a tag `v<semver>` faz (confere a versão do `.csproj`, testa, empacota, cria o GitHub Release com o `CHANGELOG`, publica no NuGet quando o secret `NUGET_USER` existe); o passo a passo (mudar `<Version>`, mover o `[Unreleased]` para `[X.Y.Z] - AAAA-MM-DD`, commit, `git tag -a vX.Y.Z -m vX.Y.Z`, `git push origin master --follow-tags`, `git ls-remote --tags origin vX.Y.Z`); o aviso do `claude-todos` sobre tag anotada; a configuração única do Trusted Publishing do nuget.org (política para o repositório e o `release.yml`, e o secret `NUGET_USER` com o nome do perfil); a configuração única do GitHub Pages (Settings → Pages → Source: GitHub Actions) e do secret `ANTHROPIC_API_KEY` para as fixtures. Uma seção **Estado** diz, com a data, que nenhuma dessas configurações foi feita e que nenhum release saiu.
- `bug_report.yml`: em português, no molde do `claude-todos`. Campos: `description`, `steps`, `library` (dropdown: .NET, Delphi, Especificação ou site), `lib-version`, `target` (texto livre: `net6.0`, `netstandard2.0`, Delphi 10.1…), `claude-version` (obrigatório: a saída de `claude --version`), `os`, `context`. O aviso do topo manda falhas de segurança para a política.
- `feature_request.yml`: os três campos do `claude-todos`.
- `config.yml`: `blank_issues_enabled: false` e o link de relato privado.

- [ ] **Step 4: Rodar e ver passar**

Run: `git add -A && python -I tools/test_repo.py -v`
Expected: tudo OK. Se o PyYAML não estiver instalado, `test_issue_templates` aparece como `skipped`: instalar com `python -m pip install pyyaml` e rodar de novo, para o teste rodar de verdade.

- [ ] **Step 5: Commit**

```bash
git commit -m "docs(release): RELEASING, CHANGELOG e modelos de issue"
```

---

### Task 3: SECURITY e CONTRIBUTING em três idiomas

**Files:**
- Create: `SECURITY.md`, `SECURITY.en.md`, `SECURITY.es.md`
- Create: `CONTRIBUTING.md`, `CONTRIBUTING.en.md`, `CONTRIBUTING.es.md`
- Modify: `tools/test_repo.py` (classe `Policies`)

**Interfaces:**
- Consumes: `read` e `LANGUAGE_LABELS` (Tarefa 1); `ADVISORIES` (Tarefa 2).

- [ ] **Step 1: Escrever os testes que falham**

```python
def test_three_languages_each_with_the_language_line(self):
    for base in ("SECURITY", "CONTRIBUTING"):
        names = [f"{base}.md", f"{base}.en.md", f"{base}.es.md"]
        for name, label in zip(names, LANGUAGE_LABELS[:3]):
            line = read(name).splitlines()[2]
            self.assertIn(f"**{label}**", line, name)
            for other, other_label in zip(names, LANGUAGE_LABELS[:3]):
                if other != name:
                    self.assertIn(f"[{other_label}]({other})", line, name)

def test_security_points_to_the_private_report_and_states_the_deadlines(self):
    for name in ("SECURITY.md", "SECURITY.en.md", "SECURITY.es.md"):
        text = read(name)
        self.assertIn(ADVISORIES, text, name)
        self.assertRegex(text, r"\b5\b", name)    # confirmação em até 5 dias úteis
        self.assertRegex(text, r"\b10\b", name)   # avaliação em até 10 dias úteis

def test_security_scope_names_what_the_library_does(self):
    for name in ("SECURITY.md", "SECURITY.en.md", "SECURITY.es.md"):
        text = read(name)
        for term in ("permission_suggestions", "CLAUDE_CONFIG_DIR", "stderr"):
            self.assertIn(term, text, name)
```

- [ ] **Step 2: Rodar e ver falhar**

Run: `python -I tools/test_repo.py Policies -v`
Expected: FAIL (`SECURITY.md` não existe).

- [ ] **Step 3: Escrever o `SECURITY.md`**

As seções do `SECURITY.md` do `claude-todos-vscode`, com este conteúdo próprio:

- **Versões suportadas:** a versão mais recente publicada no NuGet. Até a primeira publicação, o branch `master`.
- **Como reportar** e **O que esperar:** como lá (relato privado pelo GitHub, 5 dias úteis para confirmar, 10 para a avaliação inicial, advisory com crédito).
- **Escopo:**
  - o processo que a lib sobe: o executável, os argumentos e as variáveis de ambiente que ela passa, entre elas o `CLAUDE_CONFIG_DIR`;
  - injeção na linha de comando por um modelo, um modo ou um id de sessão (no Windows o `claude.cmd` passa pelo `cmd.exe`);
  - a regra da resposta de permissão: a resposta leva o `input` e as `permission_suggestions` que o lado do processo recebeu do `claude`, nunca os de quem chama. Qualquer caminho que deixe quem chama trocar um dos dois é uma falha;
  - o que a lib nunca grava em disco: a conversa, o stderr do `claude` (lido e descartado) e credenciais;
  - dado pessoal numa gravação de `fixtures/`.
- **Fora de escopo:** falhas do próprio Claude Code (vão para a Anthropic) e dependências de desenvolvimento que não entram no pacote.

- [ ] **Step 4: Escrever o `CONTRIBUTING.md`**

Seções: **Setup** (os comandos das tarefas 6 e 7: `dotnet build libs/dotnet -c Release`, `dotnet test libs/dotnet -c Release --no-build`, `npm ci` e `npm run build` em `site/`, `python -I tools/test_repo.py -v`); **Estrutura do projeto** (a árvore de pastas da spec, uma linha cada); **Como uma mudança entra** (spec em `docs/specs/`, plano em `docs/plans/`, um commit por tarefa, mensagem em português no formato do repositório); **Fixtures** (nenhuma gravação entra com dado pessoal; o teste de privacidade chega na fase 1); **Traduções** (o português é a fonte; uma mudança num README vale para os cinco; o glossário está em `docs/i18n/glossary.md`); **Estilo de código** (inglês no código e nos comentários; comentário só quando o porquê não é óbvio; TDD); **Release** (link para `RELEASING.md`).

- [ ] **Step 5: Traduzir os dois para `en` e `es`**

- [ ] **Step 6: Rodar e ver passar**

Run: `git add -A && python -I tools/test_repo.py -v`
Expected: tudo OK.

- [ ] **Step 7: Commit**

```bash
git commit -m "docs(policies): SECURITY e CONTRIBUTING em português, inglês e espanhol"
```

---

### Task 4: O README nos cinco idiomas

**Files:**
- Create: `README.md`, `README.en.md`, `README.es.md`, `README.zh-cn.md`, `README.zh-tw.md`
- Modify: `tools/test_repo.py` (classe `Readmes`)

**Interfaces:**
- Consumes: `read` e `LANGUAGE_LABELS` da Tarefa 1. Os arquivos `SECURITY*.md`, `CONTRIBUTING*.md` e `LICENSE` já existem, então os links para eles entram direto.
- Produces: em `tools/test_repo.py`, a constante `READMES` (os cinco nomes na ordem pt-BR, en, es, zh-cn, zh-tw).

- [ ] **Step 1: Escrever os testes que falham**

```python
NOTICE = {
    "README.md": "Projeto da comunidade, sem vínculo com a Anthropic nem endosso dela.",
    "README.en.md": "Community project, not affiliated with or endorsed by Anthropic.",
    "README.es.md": "Proyecto de la comunidad, sin afiliación ni respaldo de Anthropic.",
    "README.zh-cn.md": "社区项目，与 Anthropic 无隶属关系，也未获其认可。",
    "README.zh-tw.md": "社群專案，與 Anthropic 無隸屬關係，亦未獲其認可。",
}

def test_each_readme_has_the_language_line(self):
    # terceira linha: o idioma do arquivo em negrito, os outros quatro como links para os arquivos certos
    for name, label in zip(READMES, LANGUAGE_LABELS):
        line = read(name).splitlines()[2]
        self.assertIn(f"**{label}**", line, name)
        for other, other_label in zip(READMES, LANGUAGE_LABELS):
            if other != name:
                self.assertIn(f"[{other_label}]({other})", line, name)

def test_each_readme_carries_the_notice(self):
    for name, sentence in NOTICE.items():
        self.assertIn(sentence, read(name), name)

def test_all_readmes_have_the_same_sections(self):
    counts = {name: sum(1 for l in read(name).splitlines() if l.startswith("## ")) for name in READMES}
    self.assertEqual(len(set(counts.values())), 1, counts)

def test_no_readme_claims_a_published_package(self):
    # nada foi publicado na fase 0: nenhuma instrução de instalação nem badge do NuGet
    for name in READMES:
        text = read(name)
        self.assertNotIn("dotnet add package", text, name)
        self.assertNotIn("img.shields.io/nuget", text, name)
```

- [ ] **Step 2: Rodar e ver falhar**

Run: `python -I tools/test_repo.py Readmes -v`
Expected: FAIL (`README.md` não existe).

- [ ] **Step 3: Escrever o `README.md` em português**

Estrutura, nesta ordem (as seções `##` são as mesmas nos cinco):

1. `# claude-cli-protocol`
2. A linha de idiomas (terceira linha do arquivo).
3. Badges: CI (`actions/workflows/ci.yml/badge.svg`) e licença MIT. Sem badge do NuGet.
4. Um parágrafo: o protocolo stream-json do `claude -p` (Claude Code) especificado, gravado e implementado: a especificação escrita, as gravações que a provam e as libs que a falam (.NET primeiro, Delphi depois).
5. O aviso, em citação (`>`).
6. `## Estado`: fase 0. O repositório tem a estrutura, a documentação e o CI; a lib .NET chega na fase 1, a partir do `Protocol/` do Claude Visual Chat. Nada publicado ainda.
7. `## O que tem aqui`: tabela das pastas (`site/`, `docs/`, `fixtures/`, `libs/dotnet/`, `libs/delphi/`, `examples/`, `tools/`), uma linha cada.
8. `## As camadas`: o diagrama de três camadas da spec e a frase "tela nenhuma entra aqui".
9. `## Como o claude -p é chamado`: os argumentos da spec, num bloco de código, e uma frase sobre uma linha JSON por mensagem em cada sentido.
10. `## Segurança`: a regra da resposta de permissão em duas frases e o link para `SECURITY.md`.
11. `## Roteiro`: a tabela de fases só com o que é deste repositório (0, 1, 2, 5, 6) e uma linha dizendo onde ficam a 3, a 4 e a 7.
12. `## Contribuindo`: link para `CONTRIBUTING.md`.
13. `## Licença`: `[MIT](LICENSE)`.

- [ ] **Step 4: Traduzir para `en`, `es`, `zh-cn` e `zh-tw`**

Cada tradução mantém a estrutura, os blocos de código e os identificadores (`claude -p`, `stream-json`, nomes de pastas e de arquivos) como estão. Os links para SECURITY e CONTRIBUTING apontam para a versão do idioma quando ela existe (`SECURITY.en.md`, `SECURITY.es.md`); os dois READMEs em chinês apontam para as versões em inglês. O `zh-cn` e o `zh-tw` são escritos em separado, com o vocabulário de cada um (`软件`/`軟體`, `默认`/`預設`), não convertidos um do outro.

- [ ] **Step 5: Rodar e ver passar**

Run: `git add -A && python -I tools/test_repo.py -v`
Expected: tudo OK, com os 4 testes de `Readmes`.

- [ ] **Step 6: Commit**

```bash
git commit -m "docs(readme): README nos cinco idiomas, com o português como padrão"
```

---

### Task 5: As pastas da spec, o ROADMAP e o glossário

**Files:**
- Create: `fixtures/README.md`, `examples/README.md`, `libs/delphi/README.md`
- Create: `docs/ROADMAP.md`, `docs/i18n/glossary.md`
- Modify: `tools/test_repo.py` (classe `Layout`)

- [ ] **Step 1: Escrever os testes que falham**

```python
def test_spec_folders_exist_with_a_readme(self):
    for rel in ("fixtures/README.md", "examples/README.md", "libs/delphi/README.md"):
        self.assertTrue((ROOT / rel).is_file(), rel)

def test_delphi_readme_states_the_floor_and_what_stays_out(self):
    text = read("libs/delphi/README.md")
    self.assertIn("10.1 Berlin", text)
    self.assertIn("VCL", text)

def test_glossary_covers_the_five_languages(self):
    header = next(l for l in read("docs/i18n/glossary.md").splitlines() if l.startswith("| "))
    for column in ("pt-BR", "en", "es", "zh-CN", "zh-TW"):
        self.assertIn(column, header)

def test_roadmap_has_the_status_legend(self):
    self.assertIn("Status", read("docs/ROADMAP.md"))
```

- [ ] **Step 2: Rodar e ver falhar**

Run: `python -I tools/test_repo.py Layout -v`
Expected: FAIL (`fixtures/README.md` não existe).

- [ ] **Step 3: Escrever os cinco arquivos, em português**

- `fixtures/README.md`: o que entra aqui (as gravações `<cenário>.stream.ndjson` e `<cenário>.transcript.jsonl` e o resultado esperado de cada uma), de onde vêm (o gravador do CVC; chegam na fase 1), a regra de privacidade e que a pasta está vazia hoje.
- `examples/README.md`: o exemplo de console .NET chega na fase 2; o de Delphi, na fase 6.
- `libs/delphi/README.md`: a lib chega na fase 6; piso Delphi 10.1 Berlin, sem variável inline nem inferência de tipo; processo por `CreateProcess` com pipes, sem pacote de terceiros; passa nas mesmas fixtures; o componente VCL fica em repositório próprio.
- `docs/ROADMAP.md`: documento vivo no molde do `claude-todos`: o que ele rastreia (mudanças do `claude -p` e do stream-json que quebram ou pedem algo das libs), a legenda de status (🔍 a investigar · 📐 a planejar · 🚧 em andamento · ✅ entregue · ⏸️ adiado · ❄️ descartado), a seção **Vigia** (como rodar o `docs/sweep_issues.py`, que chega na Tarefa 9) e a seção **Varreduras**, vazia.
- `docs/i18n/glossary.md`: no molde do `glossary-zh.md`. A tabela de termos com as colunas `Term | pt-BR | en | es | zh-CN | zh-TW | Notes` para: session, transcript, stream, event, command, transport, permission request, tool call, control request, fixture, recording, turn, token, agent, sub-agent, hook, plan mode, default. Os termos em chinês que o glossário do `claude-todos` já fixou (会话/工作階段, 对话记录/對話記錄, 智能体/智慧體, 令牌/權杖, 钩子/掛鉤, 计划/計畫, 默认/預設) valem iguais aqui. Depois: **Never translated** (`claude -p`, `stream-json`, `control_request`, `can_use_tool`, nomes de tipos e de arquivos) e **Where the texts live** (os READMEs, SECURITY, CONTRIBUTING e `site/src/content/docs/`). O aviso de que as traduções são de IA e não foram revisadas por falante nativo vem no topo.

- [ ] **Step 4: Rodar e ver passar**

Run: `git add -A && python -I tools/test_repo.py -v`
Expected: tudo OK.

- [ ] **Step 5: Commit**

```bash
git commit -m "docs: pastas da spec (fixtures, examples, libs/delphi), ROADMAP e glossário"
```

---

### Task 6: A solução .NET vazia nos dois targets

**Files:**
- Create: `libs/dotnet/ClaudeCliProtocol.sln`
- Create: `libs/dotnet/Directory.Build.props`
- Create: `libs/dotnet/README.md` (o README do pacote, em inglês)
- Create: `libs/dotnet/src/ClaudeCliProtocol/ClaudeCliProtocol.csproj`
- Create: `libs/dotnet/src/ClaudeCliProtocol/IsExternalInit.cs`
- Create: `libs/dotnet/tests/ClaudeCliProtocol.Tests/ClaudeCliProtocol.Tests.csproj`
- Create: `libs/dotnet/tests/ClaudeCliProtocol.Tests/PackageShapeTests.cs`
- Create: `tools/check_package.py`

**Interfaces:**
- Produces: os comandos que o CI e a fase 1 usam:
  - `dotnet build libs/dotnet -c Release`
  - `dotnet test libs/dotnet -c Release --no-build`
  - `dotnet pack libs/dotnet/src/ClaudeCliProtocol -c Release --no-build -o artifacts`
  - `python -I tools/check_package.py artifacts/ClaudeCliProtocol.<versão>.nupkg` (sai com 0 e imprime `package ok`, ou sai com 1 e diz o que faltou)
- Produces: o assembly `ClaudeCliProtocol`, com `InternalsVisibleTo("ClaudeCliProtocol.Tests")`.

- [ ] **Step 1: Escrever os testes que falham**

`PackageShapeTests.cs`, namespace `ClaudeCliProtocol.Tests`:

```csharp
private static readonly Assembly Library = Assembly.Load("ClaudeCliProtocol");

[Fact]
public void Each_test_runtime_loads_its_own_build_of_the_library()
{
    var framework = Library.GetCustomAttribute<TargetFrameworkAttribute>()!.FrameworkName;
#if NET48
    Assert.Equal(".NETStandard,Version=v2.0", framework);
#else
    Assert.Equal(".NETCoreApp,Version=v6.0", framework);
#endif
}

[Fact]
public void The_library_references_no_ui_package()
{
    var names = Library.GetReferencedAssemblies().Select(a => a.Name!).ToList();
    Assert.DoesNotContain(names, n => n.StartsWith("Avalonia") || n.StartsWith("SkiaSharp") || n.StartsWith("Markdown"));
}
```

- [ ] **Step 2: Criar a solução e o projeto de teste, e ver falhar**

- `Directory.Build.props`: o do CVC (`LangVersion` 10.0, `Nullable` enable, `ImplicitUsings` disable, `TreatWarningsAsErrors` true, `ContinuousIntegrationBuild` no GitHub Actions) mais `<CheckEolTargetFramework>false</CheckEolTargetFramework>`, com o comentário `net6.0 is a deliberate target: see docs/specs`.
- `ClaudeCliProtocol.Tests.csproj`: `<TargetFrameworks>net6.0</TargetFrameworks>` e, com `Condition="$([MSBuild]::IsOSPlatform('Windows'))"`, `net6.0;net48`; `IsPackable` false; `Microsoft.NET.Test.Sdk` 17.11.1, `xunit` 2.9.2, `xunit.runner.visualstudio` 2.8.2 (as versões do CVC) e `Microsoft.NETFramework.ReferenceAssemblies` 1.0.3 com `PrivateAssets="all"`; referência de projeto para a lib.

Run: `dotnet build libs/dotnet -c Release`
Expected: FAIL, o projeto `ClaudeCliProtocol.csproj` não existe.

- [ ] **Step 3: Criar o projeto da lib**

`ClaudeCliProtocol.csproj`:

- `TargetFrameworks` `netstandard2.0;net6.0`, `RootNamespace` e `AssemblyName` `ClaudeCliProtocol`, `GenerateDocumentationFile` true com `NoWarn` `CS1591`.
- Pacote: `IsPackable` true, `PackageId` `ClaudeCliProtocol`, `Version` `0.1.0`, `Authors` `carlosdealmeida`, `PackageLicenseExpression` `MIT`, `Copyright` `Copyright (c) 2026 carlosdealmeida`, `RepositoryUrl` e `PackageProjectUrl` `https://github.com/carlosdealmeida/claude-cli-protocol`, `RepositoryType` `git`, `PackageReadmeFile` `README.md`, `PublishRepositoryUrl`, `IncludeSymbols` e `SymbolPackageFormat` `snupkg`, `PackageTags` `claude claude-code stream-json protocol cli`.
- `Description`: `Reads and writes the stream-json protocol of claude -p (Claude Code): typed events, commands, a session and swappable transports. Community project, not affiliated with or endorsed by Anthropic.`
- `PackageReference` `System.Text.Json` 8.0.5; `Microsoft.Bcl.AsyncInterfaces` 8.0.0 com `Condition="'$(TargetFramework)' == 'netstandard2.0'"`.
- `<None Include="..\..\README.md" Pack="true" PackagePath="\" Visible="false" />` e `<InternalsVisibleTo Include="ClaudeCliProtocol.Tests" />`.

`IsExternalInit.cs`: o polyfill do CVC, dentro de `#if NETSTANDARD2_0`.

`libs/dotnet/README.md`, em inglês: o que o pacote é, o aviso, "Status: no release yet; the code arrives in phase 1", os dois targets e as dependências, e o link para o repositório, onde está a documentação nos cinco idiomas. Links absolutos, porque o nuget.org não resolve os relativos.

- [ ] **Step 4: Rodar e ver passar**

Run: `dotnet build libs/dotnet -c Release && dotnet test libs/dotnet -c Release --no-build`
Expected: build sem avisos; 2 testes passando em `net6.0` e 2 em `net48` (no Windows).

- [ ] **Step 5: Escrever o `tools/check_package.py` e conferir o pacote**

O script abre o `.nupkg` (um zip) e falha se faltar qualquer um destes:

- `lib/netstandard2.0/ClaudeCliProtocol.dll`, `lib/net6.0/ClaudeCliProtocol.dll` e `README.md`;
- no `.nuspec`: `<license type="expression">MIT</license>`; a descrição com `not affiliated with or endorsed by Anthropic`; o grupo `.NETStandard2.0` com exatamente `System.Text.Json` 8.0.5 e `Microsoft.Bcl.AsyncInterfaces` 8.0.0; o grupo `net6.0` com exatamente `System.Text.Json` 8.0.5.

Run: `dotnet pack libs/dotnet/src/ClaudeCliProtocol -c Release --no-build -o artifacts && python -I tools/check_package.py artifacts/ClaudeCliProtocol.0.1.0.nupkg`
Expected: `package ok`.

Depois, provar que o script acusa erro: rodar com um zip qualquer que não seja o pacote (`python -I tools/check_package.py artifacts/ClaudeCliProtocol.0.1.0.snupkg`).
Expected: sai com 1 e nomeia o que faltou.

- [ ] **Step 6: Commit**

```bash
git add -A && python -I tools/test_repo.py -v
git commit -m "feat(dotnet): solução vazia nos targets netstandard2.0 e net6.0, com teste e conferência do pacote"
```

---

### Task 7: O site em Astro + Starlight nos cinco idiomas

**Files:**
- Create: `site/package.json`, `site/package-lock.json`, `site/astro.config.mjs`, `site/tsconfig.json`, `site/.gitignore`
- Create: `site/src/content.config.ts`
- Create: `site/src/content/docs/index.mdx` e `site/src/content/docs/overview.md` (pt-BR, na raiz)
- Create: os mesmos dois arquivos em `site/src/content/docs/en/`, `es/`, `zh-cn/` e `zh-tw/`
- Create: `site/scripts/check-dist.mjs`

**Interfaces:**
- Produces: `npm run build` (gera `site/dist/`) e `npm run check` (roda o `check-dist.mjs`), os dois dentro de `site/`.

**Pré-requisito:** o Astro 7.4.2 em diante pede Node 22.23.4 ou mais novo; a máquina onde este plano foi escrito tinha o 22.23.3 (`node --version`). Antes de começar, instalar um Node 22 que atenda (`nvm install` e `nvm use`) ou, se o dono do projeto preferir não mexer no Node agora, fixar `"astro": "7.4.1"` (que aceita o 22.22.0 em diante) e anotar isso no `package.json`.

- [ ] **Step 1: Escrever a conferência que falha**

`site/scripts/check-dist.mjs` percorre os cinco locales e falha (sai com 1, dizendo qual) se:

```js
const LOCALES = [
  { path: '', lang: 'pt-BR' },
  { path: 'en/', lang: 'en' },
  { path: 'es/', lang: 'es' },
  { path: 'zh-cn/', lang: 'zh-CN' },
  { path: 'zh-tw/', lang: 'zh-TW' },
];
const BASE = '/claude-cli-protocol/';
// para cada locale:
//   dist/<path>index.html e dist/<path>overview/index.html existem
//   o <html> de cada uma tem lang="<lang>"
//   todo href que começa com "/" começa com BASE
```

No fim imprime `site ok: 5 locales, 10 pages`.

- [ ] **Step 2: Criar o projeto e ver a conferência falhar**

`site/package.json`: `name` `claude-cli-protocol-site`, `private`, `type` `module`, scripts `dev`, `build` (`astro build`), `preview` e `check` (`node scripts/check-dist.mjs`); `devDependencies`: `astro` `^7.4.13`, `@astrojs/starlight` `^0.43.1`, `typescript` `^5.9.3`.

Run: `cd site && npm install && npm run check`
Expected: FAIL, `dist/index.html` não existe.

- [ ] **Step 3: Configurar o Starlight**

Antes de escrever, ler a documentação de i18n que vem no pacote instalado (`site/node_modules/@astrojs/starlight/`), porque a API muda entre versões.

`site/astro.config.mjs`:

- `site: 'https://carlosdealmeida.github.io'`, `base: '/claude-cli-protocol'`.
- Starlight com `title: 'claude-cli-protocol'`, o link social do GitHub, `defaultLocale: 'root'` e os locales `root` (`Português`, `pt-BR`), `en` (`English`, `en`), `es` (`Español`, `es`), `zh-cn` (`简体中文`, `zh-CN`) e `zh-tw` (`繁體中文`, `zh-TW`).
- `sidebar` com um item, `overview`, com o rótulo traduzido nos cinco.

`site/src/content.config.ts`: a coleção `docs` com o loader e o schema do Starlight.

`site/.gitignore`: `dist/`, `node_modules/`, `.astro/`.

- [ ] **Step 4: Escrever as duas páginas, em português**

- `index.mdx`: `template: splash`; o herói com o nome, a frase "O protocolo stream-json do `claude -p`, especificado, gravado e implementado" e dois botões (Visão geral; GitHub); três cartões (Especificação, Fixtures, Libs), cada um dizendo em que fase chega; o aviso de projeto da comunidade.
- `overview.md`: o que é o repositório, as três camadas, o estado (fase 0, nada publicado) e a tabela de fases.

Esta é a landing da fase 0. A landing própria em Astro, com o componente que toca uma fixture, é a fase 5.

- [ ] **Step 5: Traduzir as duas páginas para `en`, `es`, `zh-cn` e `zh-tw`**

Mesmo nome de arquivo em cada pasta de locale, para o Starlight ligar as versões.

- [ ] **Step 6: Rodar e ver passar**

Run: `cd site && npm run build && npm run check`
Expected: build sem erro e `site ok: 5 locales, 10 pages`.

- [ ] **Step 7: Commit**

```bash
git add -A && python -I tools/test_repo.py -v
git commit -m "feat(site): site em Astro + Starlight nos cinco idiomas, com o português na raiz"
```

---

### Task 8: Os quatro workflows

**Files:**
- Create: `.github/workflows/ci.yml`, `.github/workflows/release.yml`, `.github/workflows/pages.yml`, `.github/workflows/fixtures.yml`
- Modify: `tools/test_repo.py` (classe `Workflows`)

**Interfaces:**
- Consumes: os comandos das tarefas 6 e 7 e `load_yaml` da Tarefa 2.

- [ ] **Step 1: Escrever os testes que falham**

```python
WORKFLOWS = ("ci.yml", "release.yml", "pages.yml", "fixtures.yml")
ALLOWED_SECRETS = {"ANTHROPIC_API_KEY", "NUGET_USER", "GITHUB_TOKEN"}

def test_the_four_workflows_parse_and_declare_permissions(self):
    for name in WORKFLOWS:
        doc = load_yaml(f".github/workflows/{name}")
        self.assertIn("permissions", doc, name)
        self.assertIn("jobs", doc, name)

def test_workflows_only_name_known_secrets(self):
    for name in WORKFLOWS:
        used = set(re.findall(r"secrets\.([A-Za-z_][A-Za-z0-9_]*)", read(f".github/workflows/{name}")))
        self.assertLessEqual(used, ALLOWED_SECRETS, name)

def test_the_api_key_is_named_only_by_the_fixtures_workflow(self):
    for name in WORKFLOWS:
        has = "secrets.ANTHROPIC_API_KEY" in read(f".github/workflows/{name}")
        self.assertEqual(has, name == "fixtures.yml", name)

def test_no_api_key_in_the_repo(self):
    pattern = re.compile(r"sk-ant-[A-Za-z0-9_-]{20,}")
    for rel in tracked():
        data = (ROOT / rel).read_bytes()
        if b"\0" in data[:8000]:
            continue
        self.assertIsNone(pattern.search(data.decode("utf-8", "replace")), rel)

def test_branch_filters_name_master(self):
    for name in ("ci.yml", "pages.yml"):
        self.assertIn("master", read(f".github/workflows/{name}"), name)

def test_ci_runs_what_the_docs_tell_a_contributor_to_run(self):
    ci = read(".github/workflows/ci.yml")
    for command in ("dotnet build libs/dotnet -c Release", "dotnet test libs/dotnet -c Release --no-build",
                    "tools/check_package.py", "tools/test_repo.py", "npm run build", "npm run check"):
        self.assertIn(command, ci)
```

- [ ] **Step 2: Rodar e ver falhar**

Run: `python -I tools/test_repo.py Workflows -v`
Expected: FAIL (`ci.yml` não existe).

- [ ] **Step 3: Conferir a versão das actions**

Run, para cada uma de `actions/checkout`, `actions/setup-dotnet`, `actions/setup-node`, `actions/setup-python`, `actions/configure-pages`, `actions/upload-pages-artifact`, `actions/deploy-pages`, `softprops/action-gh-release`, `NuGet/login`:
`gh api repos/<action>/releases/latest --jq .tag_name`

Usar o major que o comando devolver. Os dois repositórios de referência usam `checkout@v7`, `setup-dotnet@v6`, `setup-python@v7`, `setup-node@v4`, `configure-pages@v5`, `upload-pages-artifact@v3`, `deploy-pages@v4`, `action-gh-release@v2` e `NuGet/login@v1`.

- [ ] **Step 4: Escrever os quatro workflows**

Comentários em inglês. `permissions: contents: read` no topo de cada um, e cada job pede só o que precisa a mais.

- **`ci.yml`** — em `push` e `pull_request` para `master`. Três jobs:
  - `dotnet`: matriz `windows-latest` e `ubuntu-latest`; `setup-dotnet` com `6.0.x` e `8.0.x`; build, test, pack e `check_package.py`.
  - `site`: Node 22; `npm ci`, `npm run build` e `npm run check` em `site/`.
  - `repo`: Python 3, `pip install pyyaml`, `python -I tools/test_repo.py -v`.
  - Um comentário marca onde entra o job da lib Delphi (fase 6).
- **`release.yml`** — na tag `v*`. Confere a tag contra `dotnet msbuild libs/dotnet/src/ClaudeCliProtocol -getProperty:Version`; build, test, pack, `check_package.py`; cria o GitHub Release com `body_path: CHANGELOG.md` e os `.nupkg` e `.snupkg` anexados (`contents: write` só nesse job); publica no NuGet por Trusted Publishing (`id-token: write`, `NuGet/login` com `secrets.NUGET_USER`) só quando o secret existe. Sem ele, o passo é pulado e o pacote fica no Release. Um comentário marca onde entra o zip da lib Delphi.
- **`pages.yml`** — em `push` para `master` com mudança em `site/**` ou no próprio arquivo, e `workflow_dispatch`. `concurrency: pages` sem cancelar; build de `site/`, `upload-pages-artifact` de `site/dist` e `deploy-pages`. Permissões `pages: write` e `id-token: write`.
- **`fixtures.yml`** — `schedule` com `cron: '0 6 * * 1'` (segunda-feira, 06:00 UTC) e `workflow_dispatch`; `windows-latest`, porque o gravador troca caminhos do Windows. Passos:
  1. Um guarda: se `tools/gravar.py` não existe, ou se o secret está vazio, escreve um `::notice::` dizendo qual dos dois e termina sem erro. O gravador chega na fase 2.
  2. Node 22 e `npm install -g @anthropic-ai/claude-code`.
  3. `python -I tools/gravar.py --todos --saida fixtures`, com `ANTHROPIC_API_KEY: ${{ secrets.ANTHROPIC_API_KEY }}` só nesse passo.
  4. Se `git status --porcelain fixtures` mostrar mudança: branch `fixtures/<data ISO>`, commit e `gh pr create` com o `GITHUB_TOKEN`.
  - Permissões do job: `contents: write` e `pull-requests: write`.

- [ ] **Step 5: Rodar e ver passar**

Run: `git add -A && python -I tools/test_repo.py -v`
Expected: tudo OK, com `Workflows` rodando de verdade (PyYAML instalado, nenhum `skipped`).

- [ ] **Step 6: Anotar o que não rodou**

No `RELEASING.md`, seção **Estado**: os quatro workflows foram escritos e conferidos como YAML em 2026-11-22; nenhum rodou no GitHub ainda; os passos 2 a 4 do `fixtures.yml` só rodam a partir da fase 2.

- [ ] **Step 7: Commit**

```bash
git add -A
git commit -m "chore: workflows ci, release, pages e fixtures"
```

---

### Task 9: O vigia de quebra

**Files:**
- Create: `docs/sweep_issues.py`, `docs/test_sweep_issues.py`
- Modify: `docs/ROADMAP.md` (a seção **Vigia** e a primeira entrada de **Varreduras**)
- Modify: `.github/workflows/ci.yml` (o job `repo` roda também `python -I docs/test_sweep_issues.py -v`)

**Interfaces:**
- Produces: `python docs/sweep_issues.py [AAAA-MM-DD]` (issues criadas depois da data; sem data, o histórico) e `python docs/sweep_issues.py --changelog <versão>` (as linhas do CHANGELOG do `anthropics/claude-code` depois daquela versão que casam com os termos). Precisa do `gh` autenticado.
- Produces: as funções puras `is_noise(title) -> bool`, `matches_protocol(text) -> bool`, `rank(issues, known) -> list` e `changelog_hits(markdown, since_version) -> list[tuple[str, str]]`.

- [ ] **Step 1: Ler o original inteiro**

`<refs>/claude-todos-vscode/docs/sweep_issues.py` (296 linhas): a forma das consultas, o filtro `NOISE`, a lista `KNOWN`, a espera entre consultas e a saída.

- [ ] **Step 2: Escrever os testes que falham**

`docs/test_sweep_issues.py`:

```python
def test_protocol_terms_are_recognised(self):
    for text in ("--output-format stream-json drops the result line", "control_request can_use_tool never answered",
                 "claude -p hangs with --input-format stream-json", "--permission-prompt-tool stdio",
                 "--include-partial-messages emits no stream_event"):
        self.assertTrue(matches_protocol(text), text)
    self.assertFalse(matches_protocol("Sidebar icon is misaligned in the desktop app"))

def test_billing_noise_is_dropped(self):
    self.assertTrue(is_noise("Usage credits required after upgrading"))
    self.assertFalse(is_noise("stream-json output is missing usage"))

def test_rank_drops_known_and_noise_and_sorts_by_reactions(self):
    issues = [{"number": 1, "title": "stream-json a", "reactions": 3},
              {"number": 2, "title": "stream-json b", "reactions": 9},
              {"number": 3, "title": "Usage credits required", "reactions": 99},
              {"number": 4, "title": "stream-json c", "reactions": 50}]
    self.assertEqual([i["number"] for i in rank(issues, known={4})], [2, 1])

def test_changelog_hits_start_after_the_given_version(self):
    md = "## 2.1.312\n- Fixed stream-json result line\n- New sidebar\n## 2.1.311\n- control_request change\n## 2.1.310\n- stream-json old\n"
    self.assertEqual(changelog_hits(md, "2.1.310"),
                     [("2.1.312", "Fixed stream-json result line"), ("2.1.311", "control_request change")])
```

Run: `python -I docs/test_sweep_issues.py -v`
Expected: FAIL (o módulo não existe).

- [ ] **Step 3: Escrever o `docs/sweep_issues.py`**

A estrutura do original, com:

- consultas por `stream-json`, `"--output-format"`, `"--input-format"`, `"print mode"`, `control_request`, `can_use_tool`, `"permission-prompt-tool"`, `"include-partial-messages"`, `"headless"` e `"Agent SDK" stream`;
- o mesmo filtro de ruído de cobrança do original;
- `KNOWN` vazio (nenhuma issue foi tratada ainda);
- o modo `--changelog`, que baixa o `CHANGELOG.md` do `anthropics/claude-code` pelo `gh api` e passa por `changelog_hits`.

Docstring e comentários em inglês.

- [ ] **Step 4: Rodar os testes e uma varredura de verdade**

Run: `python -I docs/test_sweep_issues.py -v`
Expected: 4 testes OK.

Run: `python docs/sweep_issues.py 2026-11-01` e `python docs/sweep_issues.py --changelog 2.1.310`
Expected: uma tabela de candidatos e a lista de linhas do CHANGELOG. Se o `gh` recusar por limite de consultas, anotar isso e não inventar resultado.

- [ ] **Step 5: Registrar no ROADMAP**

Em **Varreduras**, a entrada `### 2026-11-22`: os comandos rodados, quantos candidatos vieram e as linhas do CHANGELOG que tocam o protocolo, sem triagem ainda (🔍). Só o que a varredura devolveu de fato.

- [ ] **Step 6: Commit**

```bash
git add -A && python -I tools/test_repo.py -v
git commit -m "feat(docs): vigia de quebra das issues e do CHANGELOG do claude-code"
```

---

### Task 10: Conferência final e, com o ok do dono do projeto, o primeiro push

**Files:**
- Modify: `CHANGELOG.md` (se algo mudou em relação ao item do `[Unreleased]`)

- [ ] **Step 1: Rodar tudo de novo, do zero**

```bash
python -I tools/test_repo.py -v
python -I docs/test_sweep_issues.py -v
dotnet build libs/dotnet -c Release
dotnet test libs/dotnet -c Release --no-build
dotnet pack libs/dotnet/src/ClaudeCliProtocol -c Release --no-build -o artifacts
python -I tools/check_package.py artifacts/ClaudeCliProtocol.0.1.0.nupkg
cd site && npm ci && npm run build && npm run check
```

Expected: tudo verde. `git status` limpo depois (o `artifacts/` e o `site/dist/` estão no `.gitignore`).

- [ ] **Step 2: Parar e mostrar ao dono do projeto**

Mostrar a árvore de arquivos, a saída dos comandos e o que não foi provado: os workflows nunca rodaram no GitHub, o site não está no ar e nada foi publicado.

- [ ] **Step 3: Só com o ok dele: push e CI**

O repositório no GitHub está privado e vazio (conferido em 2026-11-22). Tornar público, ligar o Pages e criar os secrets são decisões dele.

```bash
git push -u origin master
gh run watch
```

Expected: o `ci.yml` verde nos três jobs. Se algum falhar, corrigir a causa (não o teste) e fazer um commit por correção. Atualizar a seção **Estado** do `RELEASING.md` com o que rodou e a data.
