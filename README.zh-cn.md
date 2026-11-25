# claude-cli-protocol

[Português](README.md) · [English](README.en.md) · [Español](README.es.md) · **简体中文** · [繁體中文](README.zh-tw.md)

> 本译文由 AI 生成，尚未经母语者审校。欢迎指正，术语表见 [docs/i18n/glossary.md](docs/i18n/glossary.md)。

[![CI](https://github.com/carlosdealmeida/claude-cli-protocol/actions/workflows/ci.yml/badge.svg)](https://github.com/carlosdealmeida/claude-cli-protocol/actions/workflows/ci.yml)
[![License: MIT](https://img.shields.io/badge/License-MIT-yellow.svg)](LICENSE)

**`claude -p` 的 stream-json 协议：有规范、有录制、有实现。**
[Claude Code](https://github.com/anthropics/claude-code) 以 `-p` 运行时，每条消息是一行 JSON。
本仓库把目前分散的三样东西放在一起：这个协议的书面规范、用来验证规范的真实会话录制，以及实现该协议的库
（先是 .NET，然后是 Delphi）。

> 社区项目，与 Anthropic 无隶属关系，也未获其认可。

## 状态

**版本 0.1.0，2026-11-24。** .NET 库已发布到 NuGet：`dotnet add package ClaudeCliProtocol`（[nuget.org](https://www.nuget.org/packages/ClaudeCliProtocol)）。它能读写该协议，并能启动本机的 `claude -p`：事件、两个解析器、命令、会话、录制回放和 `ProcessTransport`。它在两个目标框架上通过了 `fixtures/` 中的 42 份录制。传输层使用模拟的 `claude` 进行测试；对真实的 `claude`（2.1.331，Windows）它运行过一次，发送了一条简短消息，该测试仅在明确要求时运行。网站：<https://carlosdealmeida.github.io/claude-cli-protocol/zh-cn/>，其中有协议的规范（[协议](https://carlosdealmeida.github.io/claude-cli-protocol/zh-cn/spec/protocolo/)、[事件](https://carlosdealmeida.github.io/claude-cli-protocol/zh-cn/spec/eventos/)以及另外四页）。

## 这里有什么

| 文件夹 | 内容 |
|---|---|
| `site/` | 首页和规范，使用 Astro + Starlight，五种语言 |
| `docs/` | 设计文档（spec）、计划、ROADMAP 和翻译术语表 |
| `fixtures/` | `claude -p` 的录制，以及每份录制的预期结果 |
| `libs/dotnet/` | `ClaudeCliProtocol`，面向 `netstandard2.0` 和 `net6.0` |
| `libs/delphi/` | Delphi 库，最低支持 10.1 Berlin |
| `examples/` | 控制台示例 |
| `tools/` | 仓库结构测试和软件包检查 |

## 分层

```
claude-cli-protocol（库）      ← 只负责协议；不知道任何界面
   ↑
ClaudeVisualChat.Avalonia      ← 只负责绘制；不启动进程，也不访问网络
   ↑
你的应用程序                   ← 负责集成
```

每一层只了解它下面的一层，并通过带版本号的软件包依赖。本仓库不包含任何界面，任何语言都不例外。

## 如何调用 claude -p

```
claude -p --input-format stream-json --output-format stream-json --verbose
       --include-partial-messages --permission-prompt-tool stdio --permission-mode <模式>
```

使用这些参数时，`claude` 在 stdout 上为每个事件写一行 JSON（会话开始、回复的每个片段、每次工具调用、
每个权限请求、一轮对话结束），并从 stdin 为每条命令读一行 JSON（用户消息、对权限请求的答复、
切换模式或模型、中断）。库把这些行转换为带类型的事件，把命令转换为行。

## 安全

对权限请求的答复始终携带进程一侧从 `claude` 自身收到的 `input` 和 `permission_suggestions`。发送命令的一方只能选择请求、决定和消息：答复命令中没有 `input` 字段。向 `claude` 的 stdin 写入的一方用它自己读到的请求来构造答复，每个请求只答复一次。

如需报告漏洞，请参阅[安全策略](SECURITY.en.md)（英文）。

## 路线图

| 阶段 | 产出 |
|---|---|
| 0 | 骨架：目录结构、文档、CI 和网站 |
| 1 | .NET 库：事件、解析器、命令、会话和录制回放 |
| 2 | 启动 `claude` 的传输层、控制台示例，以及每周重新录制 fixtures |
| 5 | 网站上的规范：协议、命令、控制、传输、fixtures |
| 6 | Delphi 库，通过同一套 fixtures |

第 3、4、7 阶段在其他仓库中进行：基于本库的 Claude Visual Chat、使用它的应用程序，以及 VCL 聊天组件。
完整设计见 [docs/specs/](docs/specs/)（葡萄牙语）。

## 参与贡献

请参阅 [CONTRIBUTING.en.md](CONTRIBUTING.en.md)（英文）。

## 许可证

[MIT](LICENSE)
