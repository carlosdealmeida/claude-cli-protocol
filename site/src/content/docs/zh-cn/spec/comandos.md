---
title: 命令
description: 宿主写入 claude -p 的 stdin 的内容——带文本、图片和 uuid 的用户消息——以及这一行如何编码。
---

:::note
本页由 AI 翻译，尚未经母语者审校。术语见 [docs/i18n/glossary.md](https://github.com/carlosdealmeida/claude-cli-protocol/blob/master/docs/i18n/glossary.md)。
:::

宿主通过向 stdin 每条命令写一行 JSON 来和 `claude` 通信。行有三种形式：

| `type` | 含义 | 位置 |
|---|---|---|
| `user` | 一条用户消息 | 本页 |
| `control_request` | 宿主发给 `claude` 的控制请求：`initialize`、interrupt、切换模式或模型…… | [控制](../controle/#从宿主到-claude) |
| `control_response` | 宿主对 `claude` 的请求（例如权限请求）的回复 | [控制](../controle/#从-claude-到宿主) |

`fixtures/` 中的录制只保存 stdout。本页的行来自 .NET 库的 `StdinWriter` 和录制器（`tools/gravar.py`），
录制中的消息就是录制器发送的。

## 编码

- **每条命令一行**，以 `\n` 结尾，每行写完后 flush。
- **只用 ASCII。** 超出 ASCII 的字符转义为 `\uXXXX`（`ã` 变成 `\u00E3`）。无论系统给管道什么编码，
  这一行到达 `node` 时都一样；在 Windows 上，子进程的管道不一定是 UTF-8。这是库的规则，而不是
  `claude` 的规则：它读取的是 JSON，UTF-8 的 `ã` 也是合法 JSON。库选择不依赖管道的编码。
- **不要通过关闭 stdin 来结束一轮对话。** 使用 `--input-format stream-json` 时，一轮对话之后 stdin
  结束会立刻结束进程，并丢失尚未处理的内容
  （[#111427](https://github.com/anthropics/claude-code/issues/111427)）。关闭 stdin 就是结束会话，
  只能有意为之。

## 用户消息

<!-- stdin -->
```json
{"type":"user","message":{"role":"user","content":"Say hi in one line."},"parent_tool_use_id":null,"session_id":"default"}
```

- **`message.content`** 在消息没有图片时就是文本。
- **`parent_tool_use_id`** 为 `null`：消息发给主对话。
- **`session_id`** 为 `"default"`。`claude` 不用它来选择会话：会话就是这个进程的会话，新的或
  `--resume` 指定的。真正的 id 在 `system/init` 行中给出。

同样的文本会出现在对话记录中，作为第一个 `user` 行（见[协议](../protocolo/#对话记录)）。它不会回到
stdout。

### 带图片

有图片时，`content` 变成列表：先按顺序放图片块，再放文本块。空文本不产生块。

<!-- stdin -->
```json
{"type":"user","message":{"role":"user","content":[{"type":"image","source":{"type":"base64","media_type":"image/png","data":"iVBORw0KGgo…"}},{"type":"text","text":"What two colors do you see? One line."}]},"parent_tool_use_id":null,"session_id":"default"}
```

`data` 是文件的 base64，不带 `data:` 前缀。fixture 中的 `image` 场景就是这样发送一张 96 × 48 的 PNG
图片。

### 带 `uuid`

<!-- stdin -->
```json
{"type":"user","message":{"role":"user","content":"Say hi in one line."},"parent_tool_use_id":null,"session_id":"default","uuid":"5d0c9a3e-1b7f-4a52-9e64-0c2f8b1d7a41"}
```

带上 `uuid` 后，`claude` 会用 `command_lifecycle` 行说明这条消息的去向（从 2.1.310 起）：
`command_uuid` 是发送的 uuid，`state` 为 `queued`、`started`、`completed`、`cancelled`、`discarded`
或 `refused`。`ran_ahead: true` 表示 `claude` 在工作时立刻执行了这条命令。没有任何录制包含这些行；
对应的事件是 [`command_lifecycle`](../eventos/#没有任何录制覆盖的事件)。

:::caution[绝不要用另一条消息的 `uuid` 重发]
2.1.327 修复了一个问题：进程崩溃后用同一个 uuid 重发消息，会丢失已保存的消息。每条消息有自己的
uuid；重发就是发送一条新消息，带新的 uuid。
:::

### 带 `inline_pastes`

<!-- stdin -->
```json
{"type":"user","message":{"role":"user","content":"…"},"parent_tool_use_id":null,"session_id":"default","inline_pastes":["…"]}
```

`inline_pastes` 列出仍在 `content` 中的长段粘贴文本。在对话记录中，每段长粘贴都包在一个外壳里：一行
`<pasted_content id="abcd">`（四位小写十六进制数字）、粘贴的文本、一行 `</pasted_content id="abcd">`，
库的读取器会去掉外壳，按原样显示消息。没有任何录制包含粘贴。

## 附加的文件

协议没有文件块。库使用的方式是在文本末尾每个文件加一行，写上该文件在 `claude` 所在机器上的路径：

```
总结这个文件。
[attached file: C:\demo\app\notes.md]
```

`claude` 用自己的工具从那里读取文件。回读对话的一方会把这些行从消息末尾去掉（只去掉末尾的，而且只去
掉完全符合标记格式的），并单独显示文件。这是库和 Claude Visual Chat 的约定，不是 `claude` 的约定。

## 斜杠命令

斜杠命令（`/compact`、`/context`、插件提供的命令）作为普通消息的文本发送：

<!-- stdin -->
```json
{"type":"user","message":{"role":"user","content":"/compact"},"parent_tool_use_id":null,"session_id":"default"}
```

会话提供的命令在 `initialize` 的回复（`commands`）中给出，变化时会有一行 `system/commands_changed`。
本地命令的输出通过带 `<local-command-stdout>` 的 `user` 行返回；在 `commands` 场景中，`/compact`
返回 `<local-command-stdout>Compacted </local-command-stdout>`。

:::note
在 2.1.314 之前，文本中带有 `claude` 自身标记（例如 `<bash-stdout>`）的消息不会向本地 stream-json
宿主确认。库带 `uuid` 发送这样的消息时会看到什么，尚未核对（[ROADMAP](https://github.com/carlosdealmeida/claude-cli-protocol/blob/master/docs/ROADMAP.md) 第 6 项）。
:::
