---
title: Fixtures
description: 真实 claude -p 会话的录制、每份录制的预期结果，以及库如何证明自己能说这个协议。
---

:::note
本页由 AI 翻译，尚未经母语者审校。术语见 [docs/i18n/glossary.md](https://github.com/carlosdealmeida/claude-cli-protocol/blob/master/docs/i18n/glossary.md)。
:::

fixture 是真实 `claude -p` 会话的录制，保存在
[`fixtures/`](https://github.com/carlosdealmeida/claude-cli-protocol/tree/master/fixtures) 中。**一致性以它们
为准：** 任何语言的库，只要读取每份录制并产生旁边的预期结果，就算能说这个协议。.NET 库通过了全部 50 份；
Delphi 库也将通过同样的这些。

## 文件

每个场景四个文件：

| 文件 | 含义 |
|---|---|
| `<场景>.stream.ndjson` | `claude -p` 的 stdout，每行一个 JSON |
| `<场景>.transcript.jsonl` | 同一会话的对话记录，即 Claude Code 写入磁盘的文件 |
| `<场景>.stream.expected.json` | 从 `.stream.ndjson` 得到的事件 |
| `<场景>.transcript.expected.json` | 从 `.transcript.jsonl` 得到的事件 |

录制只保存 `claude` 写出的内容。录制器向 stdin 发送了什么，写在每个场景的脚本里，即 `tools/cenarios.json`。

## 场景

| 场景 | 脚本做了什么 | 录制展示了什么 |
|---|---|---|
| `edit` | 请求一次 `Edit` 和一次 `Write`；两者都允许 | 两个权限请求、工具结果 |
| `bash` | 请求两条 shell 命令；允许 | 建议中带 `addRules` 的请求，`decision_reason_type: subcommandResults` |
| `subagent` | 请求一个读取三个文件的子智能体 | `system/task_*`、带 `agent_id` 的请求、在 `result` 之后才结束的任务 |
| `error` | 请求读取一个不存在的文件 | 一个错误的 `tool_result` |
| `denied` | 请求一次 `Write`；附带消息拒绝 | 作为错误 `tool_result` 的消息 |
| `denied-long` | 同上，消息有四行 | 带换行的长消息 |
| `interrupted` | 请求一条长命令，不回复请求就发送 `interrupt` | `control_cancel_request`、`still_queued`、`error_during_execution` 的 `result` |
| `mode-and-model` | 切换模式和模型，然后请求一次 `Write`；始终允许 | `set_permission_mode` 和 `set_model` 的回复、“始终允许” |
| `image` | 发送一张 PNG 图片和一个问题 | 带图片的 `tool_result`（2.1.286 的录制） |
| `commands` | 查询上下文使用情况，并运行 `/hello Ana` 和 `/compact` | `get_context_usage` 的回复、`<local-command-stdout>`、压缩 |
| `effort` | 切换 effort 和模型，并查询设置 | 没有正文的 `apply_flag_settings`、带 `applied` 的 `get_settings` |
| `plan` | 在计划模式下请求一个计划；切换到 `acceptEdits` 并允许 `ExitPlanMode` | 退出计划模式 |
| `plan-auto` | 同上，切换到 `auto` | 退出计划模式进入 auto 模式 |
| `plan-feedback` | 请求一个计划；附带消息拒绝 `ExitPlanMode` | 退回给模型的计划、一次 `AskUserQuestion` |
| `queued` | 在第一条消息运行命令时发送第二条消息 | 两条消息、一轮对话：一个 `system/init` 和一个 `result` |
| `queued-compact` | 不等待就发送 `/compact`，随后发送一条消息 | `compact_boundary`，对话记录中 `queued_command` 的 `attachment` |
| `hook-blocked` | 发送一个被 `UserPromptSubmit` 钩子拦截的提示 | `warning` 级别的 `system/informational` |
| `hook-ask` | `PreToolUse` 钩子在 `Write` 之前请求确认 | `decision_reason_type: hook`，带钩子的文本 |
| `error-long` | 运行一条在 stderr 上打印 40 行并以 3 退出的命令 | 很长的错误结果、来自检查的 `decision_reason` |
| `error-wrapped` | 请求对一段不存在的文本执行 `Edit` | 工具的错误 |
| `seguranca` | 请求向 `.claude/settings.local.json` 执行 `Write`；拒绝 | `decision_reason_type: safetyCheck` |
| `bash-ansi` | 运行一条带终端颜色、响铃符和零宽字符的 `printf` | 带终端控制码和不可见字符的工具结果 |
| `advisor` | 使用 `--advisor opus`，请求调用一次 advisor | `server_tool_use` 和 `advisor_tool_result` 块，结论是加密的 |
| `workflow` | 请求一个只有一个阶段、一个智能体的 workflow | 带 `local_workflow` 的 `system/background_tasks_changed`，以及 workflow 的 `system/task_*` 行 |
| `stop-task` | 在后台运行 `sleep 60`，两秒后发送 `stop_task` | 带 `status: stopped` 的 `task_notification`、空的任务列表、`stop_task` 的回复 |

共有 25 个已录制的场景。脚本中还有第 26 个 `subagent-permission`，从未录制过。二十份录制来自 Claude Code
2.1.331；`image` 的录制来自 2.1.286，因为在 2026-11-25 的重新录制中，模型没有打开图片就作答了，而之前的
录制是唯一一份工具结果中带图片的录制。
`bash-ansi`、`advisor`、`workflow` 和 `stop-task` 的录制来自 2.1.330：它们是在 Claude Visual Chat 中录制
并从那里带过来的。

## 预期结果

中立的 JSON，不包含任何只属于某一种语言的东西。完整说明见
[fixture 的 README](https://github.com/carlosdealmeida/claude-cli-protocol/blob/master/fixtures/README.md#o-formato-do-resultado-esperado-versão-1)
（葡萄牙语）；要点如下：

<!-- fixture: edit.stream.expected.json:8 -->
```json
{"line": 2, "event": "session_started", "fields": {"session_id": "8f8ab1b3-6d82-4572-9c2c-6f5f166d7bd2", "model": "claude-haiku-5-5", "working_directory": "C:\\demo\\app", "permission_mode": "default", "claude_version": "2.1.331", "terminal_commands": [], "fast_mode_state": null, "ultrathink_available": null}}
```

- **`format`: 1**，以及 `events` 列表，文件中每行一个事件。
- **`line`** 是录制中的行号，从 1 开始，不计空行。一行可以产生零个、一个或多个事件，同一行中事件的顺序
  有意义。
- **`event`** 和 **`fields`** 的键就是[事件](../eventos/)中的名称。每个事件的键集合是固定的；没有值的
  字段为 `null`。
- **枚举**用 `snake_case` 名称（`input_json`）。**时长**用整数毫秒。**日期**用 ISO 8601、UTC，恰好三位
  毫秒并以 `Z` 结尾，截断而不四舍五入。
- **图片：** `data` 变为 `{"length": <base64 的字符数>, "sha256": "<base64 文本 UTF-8 字节的小写十六进制>"}`。
- **比较是结构化的。** 键的顺序无关紧要；列表项的顺序有关。两个数相差不超过 `1e-9` 即视为相等。

## 库如何证明一致性

1. 对每个 `<场景>.stream.ndjson`：用一个新的、**在行之间保留状态**的读取器（每个 `parent_tool_use_id`
   最后一个 `message_start` 的 id）逐行读取文件。
2. 对每个 `<场景>.transcript.jsonl`：用一个**没有状态**的读取器单独读取每一行。
3. 库按上述格式写出事件，并与 `.expected.json` 做结构化比较。
4. 任何差异都是失败，并给出到差异值的路径（例如 `events[12].fields.block.input.file_path`）。

行以 `\n` 分隔；末尾的 `\n` 被忽略，空行不计。

### 通过意味着什么

50 份预期结果是由 .NET 库写出的。通过它们，证明一个库读取录制的方式与 .NET 库相同，并且行为的变化会显示
为差异。仅凭这一点，并不能证明每个字段都是对的。有三个场景（`edit`、`denied` 和 `mode-and-model`）用一个
读取原始录制的脚本从外部核对过。录制覆盖了读取器 36 种事件中的 27 种；另外九种见
[事件](../eventos/#没有任何录制覆盖的事件)。

## 脱敏

录制是真实的会话，任何录制都不能带有录制者的个人数据。录制器（`tools/gravar.py`）：

- 把文件夹、主目录和用户名替换为 `demo` 的（`C:\demo\app`、`C:\Users\demo`），即使路径被拆分在一个块的
  多个 delta 中也一样；
- 把 Claude Code 在 `Temp\claude` 下创建的项目文件夹替换为 `demo` 文件夹的名称（`C--demo-app`）：从
  2.1.330 起，这个文件夹的名称是由录制者的文件夹得出的 16 位十六进制数字；
- 把所有 `encrypted_content`（例如 advisor 结论中的）替换为 `redacted`；
- 在 `system/init` 行中只保留已知字段，并去掉 `mcp__` 工具；
- 在 `initialize`、`get_settings` 和 `get_context_usage` 的回复中只保留已知字段，并去掉账户；
- 去掉 `rate_limit_event` 行（它们属于录制者的账户）以及 `result` 中的 `credential_expires_at`。

隐私测试（`FixturePrivacyTests`）会在每个文件中查找其他用户的文件夹、以哈希命名的项目文件夹、看起来像凭据的内容和电子邮件地址，并
说明在哪一行、哪个字段找到，但不显示值。两者都不理解对话的文本：录制者在提交之前要自己读一遍录制。

## 每周重新录制

每周一，`fixtures.yml` 工作流用最新的 `claude` 重新录制已有录制的场景，分两个作业：

1. 运行真实 `claude` 的作业，没有仓库的写权限：录制、重新写出预期结果，并对新文件运行一致性测试和隐私测试；
2. 打开拉取请求的作业，把录制和预期结果一起提交，不运行 `claude`。

会话和消息的 id 每次录制都会变，所以每周都有一个拉取请求。由于预期结果是库自己写的，能拦下一份糟糕录制的，
是阅读拉取请求的人：一个事件消失了，一个字段的结构变了。**这样，CLI 的变化会先在这里出现，而不是在使用中才
被发现。** 第一次完整跑完的重新录制是 2026-11-25 的那一次。

## 手动录制

```bash
python -I tools/gravar.py edit        # 一个场景
python -I tools/gravar.py --todos     # 全部
python -I tools/test_gravar.py        # 脱敏器的测试（不启动 claude）
```

录制器在一个临时文件夹中、用本机的账户启动 PATH 中的 `claude`（或 `--claude <路径>` 指定的那个）。录制之后，
由 .NET 库重新写出预期结果：

```bash
CCP_UPDATE_EXPECTED=1 dotnet test libs/dotnet -c Release --framework net6.0 --filter "FullyQualifiedName~ConformanceTests"
```
