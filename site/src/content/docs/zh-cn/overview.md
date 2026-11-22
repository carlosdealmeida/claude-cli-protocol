---
title: 概览
description: claude-cli-protocol 是什么、如何分层，以及目前处于哪个阶段。
---

[Claude Code](https://github.com/anthropics/claude-code) 以 `-p` 运行时，每条消息是一行 JSON。
**claude-cli-protocol** 把目前分散的三样东西放在一起：

- 这个协议的书面**规范**；
- 用来验证规范的真实会话**录制**；
- 实现该协议的**库**，先是 .NET，然后是 Delphi。

社区项目，与 Anthropic 无隶属关系，也未获其认可。

## 状态

**第 0 阶段，截至 2026-11-22。** 仓库已有目录结构、文档、CI 和本网站。.NET 库目前还是空的，尚未发布任何软件包。

## 分层

```
claude-cli-protocol（库）      ← 只负责协议；不知道任何界面
   ↑
ClaudeVisualChat.Avalonia      ← 只负责绘制；不启动进程，也不访问网络
   ↑
你的应用程序                   ← 负责集成
```

每一层只了解它下面的一层，并通过带版本号的软件包依赖。本仓库不包含任何界面。

## 如何调用 claude -p

```
claude -p --input-format stream-json --output-format stream-json --verbose
       --include-partial-messages --permission-prompt-tool stdio --permission-mode <模式>
```

`claude` 在 stdout 上为每个事件写一行 JSON，并从 stdin 为每条命令读一行 JSON。

## 阶段

| 阶段 | 产出 |
|---|---|
| 0 | 骨架：目录结构、文档、CI 和网站 |
| 1 | .NET 库：事件、解析器、命令、会话和录制回放 |
| 2 | 启动 `claude` 的传输层、控制台示例，以及每周重新录制 fixtures |
| 5 | 本网站上的规范：协议、命令、控制、传输、fixtures |
| 6 | Delphi 库，通过同一套 fixtures |

第 3、4、7 阶段在其他仓库中进行。
