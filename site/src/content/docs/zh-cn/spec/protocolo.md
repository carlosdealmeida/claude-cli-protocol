---
title: 协议
description: 如何调用 claude -p、一行的格式、stdout 中各类行，以及一轮对话。
---

:::note
本页由 AI 翻译，尚未经母语者审校。术语见 [docs/i18n/glossary.md](https://github.com/carlosdealmeida/claude-cli-protocol/blob/master/docs/i18n/glossary.md)。
:::

带 `--input-format stream-json` 和 `--output-format stream-json` 的 `claude -p` 是一个进程：它在
stdin 上每条命令读一行 JSON，在 stdout 上每个事件写一行 JSON。本页说明 stdout 上输出什么。写入
stdin 的内容见[命令](../comandos/)和[控制](../controle/)；进程本身见[传输层](../transporte/)。

:::caution[该协议没有契约]
Anthropic 并未把这个协议作为稳定接口来记录
（[anthropics/claude-code#111046](https://github.com/anthropics/claude-code/issues/111046)）。这里的内容
是对照 `fixtures/` 中的录制（使用 Claude Code 2.1.331 录制，`image` 场景使用 2.1.286）以及 .NET 库的
代码核对的。新版本可能改变任何一行。每周重新录制 fixture 就是用来发出警告的。
:::

## 调用

```
claude -p --input-format stream-json --output-format stream-json --verbose
       --include-partial-messages --permission-prompt-tool stdio --permission-mode <modo>
```

- `--verbose` 是必需的：没有它，`claude -p` 会拒绝 `--output-format stream-json`。
- `--include-partial-messages` 让它在回复到达时逐段写出（即 `stream_event` 行）。
- `--permission-prompt-tool stdio` 让它在自己的 stdout 上用 `can_use_tool` 的 `control_request`
  请求权限，并在 stdin 上等待回复。

可选参数（`--resume`、`--model`、`--effort`）以及对每个值的校验见[传输层](../transporte/#参数)。

## 行

- **一行一个 JSON 对象**，以 `\n` 结尾。stdout 是 UTF-8。
- **`type` 字段**说明这一行是什么。在 `system` 中，`subtype` 说明是哪种通知；在 `stream_event`
  中是 `event.type`；在 `control_request` 中是 `request.subtype`。
- **几乎每一行都带有 `session_id` 和 `uuid`。** `session_id` 属于会话；`uuid` 属于这一行。
- **读取器不抛出异常。** 空行、不以 `{` 开头的行（去掉开头的空格和终端控制码之后）、无效的 JSON、
  未知的 `type` 或缺少字段：这一行不产生事件，读取继续。.NET 库的两个读取器都是这样做的，这也让
  `claude` 新增一种行时库依然能正常工作。
- **未知字段会被忽略。** 每行带的字段远多于读取器用到的（`result` 有二十多个）。读取器只读自己认识的。

## 行的类型

在 21 份 stdout 录制（`fixtures/*.stream.ndjson`）中的数量：

| `type` | 子类型 | 录制中出现次数 | 含义 |
|---|---|---:|---|
| `system` | `init` | 28 | 一轮对话的开始：会话、模型、文件夹、模式、版本 |
| `system` | `status` | 66 | `claude` 正在做什么（`requesting`、`compacting`、`null`）以及模式 |
| `system` | `thinking_tokens` | 28 | `claude` 在正在到达的块中到目前为止思考了多少令牌 |
| `system` | `task_started`、`task_progress`、`task_updated`、`task_notification` | 6 | 一个后台任务：子智能体或命令 |
| `system` | `compact_boundary` | 2 | 对话被压缩了 |
| `system` | `informational` | 1 | `claude` 关于自身工作的通知（例如某个钩子拦截了提示） |
| `system` | `background_tasks_changed` | 2 | 后台任务列表发生变化。库不读取 |
| `stream_event` | `message_start` | 53 | 一条回复开始到达 |
| `stream_event` | `content_block_start`、`content_block_delta`、`content_block_stop` | 1,015 | 回复的各个块，一段一段地 |
| `stream_event` | `message_delta`、`message_stop` | 106 | 回复结束。库不读取 |
| `assistant` | | 86 | 回复中一个完整的块 |
| `user` | | 45 | 返回给模型的内容：工具结果、`claude` 自己写的文本 |
| `control_request` | `can_use_tool` | 19 | 一个权限请求 |
| `control_cancel_request` | | 1 | `claude` 放弃了一个权限请求 |
| `control_response` | `success` | 36 | 对宿主发出的控制请求的回复 |
| `result` | `success`、`error_during_execution` | 28 | 一轮对话的结束 |

有些行库会读取，但没有任何录制包含。在 `system` 中：`api_retry`、`hook_started`、`hook_response`、
`commands_changed`、`bridge_state`、`local_command`、`per_turn_effort_changed`、
`model_refusal_fallback` 和 `peer_message_hold`。`system` 之外：`rate_limit_event`（录制器会把它
去掉，因为它属于录制者的账户）和 `command_lifecycle`。它们产生的事件见[事件](../eventos/)，并标注为未录制。

## 一轮对话的开始：`system/init`

`system/init` 行开启的是每一轮对话，而不是每个进程。在 21 份录制中，`system/init` 和 `result`
一一交替出现。

<!-- fixture: edit.stream.ndjson:2 -->
```json
{"type": "system", "subtype": "init", "session_id": "8f8ab1b3-6d82-4572-9c2c-6f5f166d7bd2", "model": "claude-haiku-5-5", "cwd": "C:\\demo\\app", "permissionMode": "default", "claude_code_version": "2.1.331", "tools": ["Task", "AskUserQuestion", "Bash", "…"]}
```

读取器从中取出：`session_id`、`model`、`cwd`、`permissionMode` 和 `claude_code_version`；如果有的话，
还有 `terminal_slash_commands`、`fast_mode_state` 和 `ultrathink_available`。录制只保留这一行中已知的
字段：其余的由录制器去掉（见 [Fixtures](../fixtures/#脱敏)）。

:::note
生效的模式是这一行中的模式，而不是命令行上请求的模式。从 2.1.323 起，当 auto 不可用时，
`--permission-mode auto` 会从 `default` 开始
（[#111343](https://github.com/anthropics/claude-code/issues/111343)）。
:::

## 正在到达的回复：`stream_event`

有了 `--include-partial-messages`，模型的每条回复会先以 Anthropic API 的流式格式分段到达，放在
`event` 里：

<!-- fixture: edit.stream.ndjson:5 -->
```json
{"type": "stream_event", "event": {"type": "content_block_start", "index": 0, "content_block": {"type": "tool_use", "id": "toolu_015zZi1E7vQS17Zr1yMU1XbP", "name": "Read", "input": {}}}, "parent_tool_use_id": null}
```

<!-- fixture: edit.stream.ndjson:7 -->
```json
{"type": "stream_event", "event": {"type": "content_block_delta", "index": 0, "delta": {"type": "input_json_delta", "partial_json": "{\"file_"}}, "parent_tool_use_id": null}
```

- **`message_start`** 带有 `event.message.id`，即回复的 id。后面的 delta 不带这个 id：**读取器为每个
  `parent_tool_use_id` 记住最后一个 `message_start` 的 id**，并把它放到各个块的事件上。所以 stdout
  读取器有状态，而对话记录读取器没有。
- **`content_block_start`** 打开第 `index` 个块。`tool_use` 以空的 `input`（`{}`）开始。
- **`content_block_delta`** 扩展这个块。`delta.type` 说明是哪一部分：`text_delta`（字段 `text`）、
  `thinking_delta`（字段 `thinking`，有时还有 `estimated_tokens`）或 `input_json_delta`（字段
  `partial_json`，是 `input` 的 JSON 的一段，直到最后才闭合）。
- **`content_block_stop`** 关闭这个块。
- **`message_delta`** 和 **`message_stop`** 结束这条回复。库不读取它们：`assistant` 行带有完整的块。
- **`parent_tool_use_id`** 是写这条回复的子智能体所对应的工具调用；对主对话为 `null`。

## 完整的块：`assistant`

分段之后，`claude` 以 API 的消息格式把完整回复写成 `assistant` 行：`message.id`、`message.content`
（块列表）和 `message.usage`。在录制中，每个 `assistant` 行只有一个块，有三个块的回复会变成三行，
`message.id` 相同。

<!-- fixture: edit.stream.ndjson:15 -->
```json
{"type": "assistant", "message": {"id": "msg_011ChNjwF76zsXQUUbNF7yfr", "role": "assistant", "content": [{"type": "tool_use", "id": "toolu_015zZi1E7vQS17Zr1yMU1XbP", "name": "Read", "input": {"file_path": "C:\\demo\\app\\notes.md"}}]}, "parent_tool_use_id": null}
```

读取器认识的块：`text`、`thinking`、`tool_use`、`tool_result` 和 `image`。`message.model` 等于
`<synthetic>` 表示这一行由 `claude` 自己写，而不是模型写的。

## 返回给模型的内容：`user`

工具结果通过 `user` 行返回，`message.content` 中是一个 `tool_result` 块，`tool_use_result` 中是工具的
结构化结果：

<!-- fixture: edit.stream.ndjson:19 -->
```json
{"type": "user", "message": {"role": "user", "content": [{"tool_use_id": "toolu_015zZi1E7vQS17Zr1yMU1XbP", "type": "tool_result", "content": "1\tline one\n…"}]}, "parent_tool_use_id": null, "tool_use_result": {"type": "text", "file": {"filePath": "C:\\demo\\app\\notes.md"}}, "tool_result_meta": [{"id": "toolu_015zZi1E7vQS17Zr1yMU1XbP", "permission_decision": {"decision": "accept", "source": "config", "reason_type": "mode"}}]}
```

- `tool_result` 的 `content` 是文本或块列表（文本和图片）。读取器用 `\n` 连接文本，图片单独保存。
- `is_error: true` 表示错误结果。工具没有运行时，`tool_result_meta[].non_execution_kind` 说明原因：
  `permission-rule`（被拒绝）或 `user-rejected`（被中断）。
- `claude` 也会在 `user` 行中写入自己的文本：斜杠命令的标记（`<local-command-stdout>`）、压缩后的
  摘要、`[Request interrupted by user for tool use]`。宿主发送的消息不会作为 `user` 行回到 stdout
  （只有加上 `--replay-user-messages` 才会，而库不传这个参数）。

## 一轮对话的结束：`result`

<!-- fixture: edit.stream.ndjson:75 -->
```json
{"type": "result", "subtype": "success", "is_error": false, "duration_ms": 3039, "duration_api_ms": 2777, "num_turns": 4, "total_cost_usd": 0.003943445, "usage": {"input_tokens": 6, "cache_creation_input_tokens": 25845, "cache_read_input_tokens": 50572, "output_tokens": 413}, "fast_mode_state": "off", "result": "Done. Line 3 of `notes.md` now reads `line 3 (edited)`, and `todo.md` was created with two bullet items: \"First item\" and \"Second item\"."}
```

- `subtype` 为 `success` 或某种错误；在录制中唯一的错误是 `error_during_execution`（被中断的一轮
  对话），带有 `is_error: true` 和 `errors` 列表。
- 读取器累加的输入令牌：`input_tokens` + `cache_creation_input_tokens` + `cache_read_input_tokens`。
  输出令牌：`output_tokens`。
- 失败的一轮对话的文本：有 `result` 就用它；否则用 `errors` 中的第一段文本。
- `duration_api_ms` 从进程启动时开始计算，而不是从这一轮对话开始。

:::caution
成功的 `result` 可能掩盖本轮早先的一次拒绝
（[#108132](https://github.com/anthropics/claude-code/issues/108132)）。需要知道哪些被拒绝的人，应读取
权限请求和 `tool_result`，而不只是 `result`。
:::

## 一轮对话

一轮对话从一条用户消息开始，到它的 `result` 结束：

1. 宿主把消息写入 stdin；
2. `claude` 写出 `system/init` 和 `system/status`（`requesting`）；
3. 对模型的每条回复：`stream_event`（`message_start`、各个块、`message_stop`）以及这些块的
   `assistant` 行；
4. 对每个工具：需要权限时是 `can_use_tool` 的 `control_request`，然后是带有结果的 `user` 行；
5. `result`。

**在一轮对话中途发送的消息不会开启新的一轮。** 在 `queued` 场景中发送了两条消息，第二条是在第一条
运行命令时发送的；`claude` 把第二条并入同一轮，流中只有一个 `system/init` 和一个 `result`。用已发送
的消息数去对 `result` 的数量是对不上的。见[传输层](../transporte/#忙碌)中的“忙碌”。

后台任务（`system/task_*`）可能在开启它们的那一轮的 `result` 之后才结束：在 `subagent` 场景中，
`task_notification` 出现在第 76 行，在第 72 行的 `result` 之后。

## 对话记录

Claude Code 还会把每个会话写入磁盘上的 `.jsonl` 文件（“对话记录”）。它不是 stdout：它是另一种格式，
有另外的行，用来读取已经发生过的对话。

- **类型：** `user`、`assistant`、`system` 和 `attachment`。在 21 份录制（`fixtures/*.transcript.jsonl`）
  中出现了 `user`（76）、`assistant`（84）、带 `compact_boundary`（2）和 `informational`（1）的
  `system`，以及一个 `queued_command` 的 `attachment`（1）。
- **名称不同。** 工具的结构化结果是 `toolUseResult`，而不是 `tool_use_result`；会话 id 是
  `sessionId`；压缩的元数据可能是 `compactMetadata`，带 `preTokens` 和 `postTokens`。
- **没有分段。** 没有 `stream_event`、没有 `control_request`、也没有 `result`：只有完整的块。
- **用户消息在里面**，作为带文本的 `user` 行，这在 stdout 中不会出现。一轮对话中途送达的消息以
  `attachment`（`queued_command`）的形式保存。
- **读取器没有状态：** 每一行单独读取。

<!-- fixture: edit.transcript.jsonl:1 -->
```json
{"type": "user", "uuid": "8b8a5b2f-b887-4d8f-baa5-fd637c77371a", "parentUuid": null, "isSidechain": false, "timestamp": "2026-11-25T00:59:51.422Z", "sessionId": "8f8ab1b3-6d82-4572-9c2c-6f5f166d7bd2", "message": {"role": "user", "content": "Use the Edit tool to change line 3 of notes.md to 'line 3 (edited)'. Then use the Write tool to create todo.md with two bullet items. Be brief."}}
```

每种格式产生的事件见[事件](../eventos/)，表中有来源一列。
