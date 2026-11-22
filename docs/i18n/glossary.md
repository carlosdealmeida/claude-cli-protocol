# Translation glossary

This is the terminology reference for the translations of claude-cli-protocol. Portuguese (pt-BR)
is the source language. Every README, policy file and page of the site must use the terms below
consistently.

**The translations were produced by AI and have not yet been reviewed by a native speaker.**
Corrections are very welcome, and this file is the best place to start. Fixing a term here and in
the files it appears in is more valuable than fixing individual sentences, because inconsistent
terminology is what makes a translation feel careless.

## Terms

| Term | pt-BR | en | es | zh-CN | zh-TW | Notes |
|---|---|---|---|---|---|---|
| protocol | protocolo | protocol | protocolo | 协议 | 通訊協定 | |
| specification | especificação | specification | especificación | 规范 | 規格 | The written description of the protocol |
| session | sessão | session | sesión | 会话 | 工作階段 | A Claude Code session |
| transcript | arquivo da sessão | session file | archivo de la sesión | 对话记录 | 對話記錄 | The `.jsonl` file Claude Code writes |
| event | evento | event | evento | 事件 | 事件 | One thing a line of the stream says |
| command | comando | command | comando | 命令 | 命令 | What is sent to `claude`; a *slash command* is something else |
| transport | transporte | transport | transporte | 传输层 | 傳輸層 | What carries lines and commands: a process, a relay, a recording |
| permission request | pedido de permissão | permission request | solicitud de permiso | 权限请求 | 權限請求 | The `can_use_tool` control request |
| tool call | chamada de ferramenta | tool call | llamada a una herramienta | 工具调用 | 工具呼叫 | |
| control request | pedido de controle | control request | solicitud de control | 控制请求 | 控制請求 | |
| recording | gravação | recording | grabación | 录制 | 錄製 | A file in `fixtures/` |
| fixture | fixture | fixture | fixture | fixture | fixture | Kept in English everywhere |
| turn | turno | turn | turno | 一轮对话 | 一輪對話 | From a user message to its `result` line |
| token | token | token | token | 令牌 | 權杖 | LLM token |
| agent | agente | agent | agente | 智能体 | 智慧體 | AI agent, not a network proxy: avoid 代理 |
| sub-agent | sub-agent | sub-agent | subagente | 子智能体 | 子智慧體 | |
| hook | hook | hook | hook | 钩子 | 掛鉤 | The Claude Code settings hook |
| plan mode | modo plan | plan mode | modo plan | 计划模式 | 計畫模式 | |
| default | padrão | default | por defecto | 默认 | 預設 | |
| library | lib | library | biblioteca | 库 | 程式庫 | |
| package | pacote | package | paquete | 软件包 | 套件 | A NuGet package |
| repository | repositório | repository | repositorio | 仓库 | 儲存庫 | |
| UI | tela | UI | interfaz gráfica | 界面 | 介面 | "No UI goes in this repository" |

The Chinese terms shared with [claude-todos-vscode](https://github.com/carlosdealmeida/claude-todos-vscode)
(session, transcript, agent, sub-agent, token, hook, plan, default) are the ones its glossary
already fixed, so the two projects read the same.

## Why two separate Chinese versions

Simplified and Traditional Chinese are not a character-level conversion of one another. Technical
vocabulary differs (`软件` / `軟體`, `程序` / `程式`, `默认` / `預設`), and nearly every term in this
project is technical. So `zh-cn` and `zh-tw` are written as two complete, independent texts.

## Never translated

Proper nouns and identifiers stay in their original form. Translating them breaks the
correspondence with what people see in the terminal and in the official documentation:

- `Claude Code`, `Claude Visual Chat`, `claude-cli-protocol`
- `claude -p`, `stream-json` and every command-line argument
- the names of the protocol: `control_request`, `control_response`, `can_use_tool`,
  `permission_suggestions`, `stream_event`, `result`…
- type, file and folder names: `ClaudeSession`, `fixtures/`, `README.md`
- `stdin`, `stdout`, `stderr`

## Where the texts live

| Files | Languages |
|---|---|
| `README.md`, `README.en.md`, `README.es.md`, `README.zh-cn.md`, `README.zh-tw.md` | all five |
| `SECURITY*.md`, `CONTRIBUTING*.md` | pt-BR, en, es |
| `site/src/content/docs/` (pt-BR at the root; `en/`, `es/`, `zh-cn/`, `zh-tw/`) | all five |
| `RELEASING.md`, `docs/specs/`, `docs/plans/`, `docs/ROADMAP.md` | pt-BR only |
| `CHANGELOG.md`, `libs/dotnet/README.md`, this glossary | English only |

The structure test (`tools/test_repo.py`) checks that the five READMEs have the same sections and
that every relative link resolves. Wording quality is not something a test can check: that is what
human review is for.
