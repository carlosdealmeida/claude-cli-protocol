---
title: 控制
description: 两个方向的控制请求——从宿主到 claude，从 claude 到宿主——、权限请求、回复及其规则。
---

:::note
本页由 AI 翻译，尚未经母语者审校。术语见 [docs/i18n/glossary.md](https://github.com/carlosdealmeida/claude-cli-protocol/blob/master/docs/i18n/glossary.md)。
:::

控制请求是一个带 `request_id` 的 `control_request` 行，回复是一个重复该 id 的 `control_response`
行。请求有两个方向：

- **从宿主到 `claude`**：`initialize`、interrupt、切换模式、模型和 effort、各种查询；
- **从 `claude` 到宿主**：权限请求（`can_use_tool`），以及钩子、MCP 和 elicitation 的请求，本协议
  不处理后者。

## 形式

请求：

<!-- stdin -->
```json
{"type":"control_request","request_id":"mode-0","request":{"subtype":"set_permission_mode","mode":"acceptEdits"}}
```

成功的回复，正文在 `response.response` 中（有正文时）：

<!-- fixture: mode-and-model.stream.ndjson:2 -->
```json
{"type": "control_response", "response": {"subtype": "success", "request_id": "mode-0", "response": {"mode": "acceptEdits"}}}
```

错误的回复，文本在 `response.error` 中：

<!-- stdin -->
```json
{"type":"control_response","response":{"subtype":"error","request_id":"…","error":"not supported"}}
```

- **`request_id` 由发出请求的一方决定。** 宿主为自己的请求选择 id；`claude` 为它的请求选择 id。库为
  每个请求生成新 id（32 位、不带连字符的 GUID）：调用方不选。录制器使用易读的 id（`init-1`、
  `mode-0`），所以录制中出现的是这些。
- **回复只给出 id，不说明它回复的是哪个请求。** 发出请求的一方按 id 保存请求，以便知道回复确认了什么。
  在库中，由 `ClaudeSession` 做这件事，并把这一对作为 `control_replied` 交出。
- **顺序没有保证。** 回复可能出现在一轮对话的各行之间。

## 从宿主到 claude

| `subtype` | 请求内容 | 请求正文 | 回复正文 | 是否录制 |
|---|---|---|---|---|
| `initialize` | 握手；官方 SDK 首先发送它 | — | `commands`、`models`、`account`、`auth_conflict` | 全部 |
| `interrupt` | 停止这一轮对话 | — | `still_queued`：仍在排队的消息 | `interrupted` |
| `set_permission_mode` | 切换模式 | `mode` | `mode` | `mode-and-model`、`plan*` |
| `set_model` | 切换模型 | `model` | `model`，已解析 | `mode-and-model`、`effort` |
| `apply_flag_settings` | 切换 effort | `settings.effortLevel`（文本或 `null`） | 无 | `effort` |
| `get_settings` | 当前生效的设置 | — | `applied`（`model`、`effort`） | `effort` |
| `get_context_usage` | 上下文窗口的使用情况 | — | `categories`、`totalTokens`、`maxTokens`、`percentage` | `commands` |
| `get_task_output` | 某个后台任务已输出的内容 | `task_id` | — | 否 |
| `remote_control` | 打开或关闭 Remote Control | `enabled` | 打开时为 `session_url` | 否 |
| `get_chrome_dialog` | 浏览器扩展的状态 | — | — | 否 |

### `initialize`

<!-- stdin -->
```json
{"type":"control_request","request_id":"init-1","request":{"subtype":"initialize"}}
```

回复列出会话的斜杠命令、模型和账户：

<!-- fixture: edit.stream.ndjson:1 -->
```json
{"type": "control_response", "response": {"subtype": "success", "request_id": "init-1", "response": {"commands": [{"name": "compact", "description": "Free up context by summarizing the conversation so far", "argumentHint": "<optional custom summarization instructions>", "builtin": true}, "…"]}}}
```

- `commands[]`：`name`（不带斜杠）、`description`、`argumentHint`。
- `models[]`：`value`、`resolvedModel`、`displayName`、`description` 和 `supportedEffortLevels`。
- `account`：登录方式（`apiProvider`、`tokenSource`、`apiKeySource`）、`email`、`organization` 和
  `subscriptionType`。录制中没有它：录制器会去掉，因为它是录制者的账户。
- `auth_conflict`：同时存在令牌和 API key 时，两个来源的名称（`token_source` 和 `api_key_source`），
  绝不是凭据本身。

所有录制都以第 1 行的 `initialize` 回复开始，因为录制器在任何消息之前先发送它。`system/init` 行只有
在第一条消息时才到达。

### `interrupt`

<!-- fixture: interrupted.stream.ndjson:22 -->
```json
{"type": "control_response", "response": {"subtype": "success", "request_id": "interrupt-1", "response": {"still_queued": []}}}
```

被中断的一轮对话以 `error_during_execution` 的 `result` 结束。在此之前，尚未回复的权限请求会被取消
（见 [`control_cancel_request`](#control_cancel_request)）。

### `set_permission_mode` 和 `set_model`

模式或模型从成功回复之后开始生效。`set_model` 的回复带有解析后的模型：在 `effort` 场景中，请求
`haiku` 返回的是 `claude-haiku-5-5`。

<!-- fixture: effort.stream.ndjson:14 -->
```json
{"type": "control_response", "response": {"subtype": "success", "request_id": "model-3", "response": {"model": "claude-haiku-5-5"}}}
```

库接受请求的模式为 `plan`、`default`、`acceptEdits` 和 `auto`。不在该列表中的模式不会被写出（见
[传输层](../transporte/#校验)）。

### `apply_flag_settings` 和 `get_settings`

<!-- stdin -->
```json
{"type":"control_request","request_id":"effort-1","request":{"subtype":"apply_flag_settings","settings":{"effortLevel":"high"}}}
```

回复不确认任何东西：它是一个没有正文的成功回复。当前生效的 effort 在 `get_settings` 的回复中给出：

<!-- fixture: effort.stream.ndjson:2 -->
```json
{"type": "control_response", "response": {"subtype": "success", "request_id": "effort-1"}}
```

<!-- fixture: effort.stream.ndjson:3 -->
```json
{"type": "control_response", "response": {"subtype": "success", "request_id": "settings-1", "response": {"applied": {"model": "claude-haiku-5-5", "effort": "high"}}}}
```

`effortLevel: null` 恢复为模型的默认 effort。从 2.1.325 起，无效值会以错误回复被拒绝。

### `get_context_usage`

<!-- fixture: commands.stream.ndjson:12 -->
```json
{"type": "control_response", "response": {"subtype": "success", "request_id": "context-2", "response": {"categories": [{"name": "System prompt", "tokens": 3129, "kind": "used"}, "…"], "totalTokens": 25341, "maxTokens": 1000000, "percentage": 3}}}
```

`kind` 为 `used`、`deferred`、`buffer` 或 `free`。如果有的话，`model`、`memoryFiles` 和 `agents`
补充这个回复。

### 如何读取响应体

库不知道一个回复回答的是哪个请求：它按正文的结构来读取。

| 正文中有 | 读取为 |
|---|---|
| `commands` | 斜杠命令；如果还有 `models` 或 `account`，再读取 `auth_conflict` |
| `models` | 模型 |
| `applied` | 当前生效的设置 |
| `categories` 和 `totalTokens` | 上下文的使用情况 |
| `account` | 账户 |
| `rate_limits` | 用量限制（`get_usage` 的回复；库不发这个请求，但会读取） |
| `session_url` | Remote Control 的链接 |

## 从 claude 到宿主

### `can_use_tool`，权限请求

使用 `--permission-prompt-tool stdio` 时，每个需要权限的工具都会在 stdout 上变成一个请求，`claude`
在 stdin 上等待回复：

<!-- fixture: edit.stream.ndjson:40 -->
```json
{"type": "control_request", "request_id": "0f8155c2-5135-4e0b-9da0-4cbc9d41ee1e", "request": {"subtype": "can_use_tool", "tool_name": "Edit", "display_name": "Edit", "input": {"file_path": "C:\\demo\\app\\notes.md", "old_string": "line three\n", "new_string": "line 3 (edited)\n", "replace_all": false}, "description": "notes.md", "permission_suggestions": [{"type": "setMode", "mode": "acceptEdits", "destination": "session"}], "tool_use_id": "toolu_01FPyYmYbFzFZR24ZJtHzcWB"}}
```

| 字段 | 含义 |
|---|---|
| `tool_name` | 工具。没有它，库不产生事件，传输层以错误回复 |
| `display_name`、`description` | 如何显示这个请求 |
| `input` | 工具将要做的事，与模型请求的一致 |
| `permission_suggestions` | `claude` 建议保存的规则，以免再次询问 |
| `tool_use_id` | 该请求对应的工具调用 |
| `agent_id` | 发出请求的子智能体（它的 `task_id`）；主对话中没有 |
| `decision_reason`、`decision_reason_type`、`decision_reason_code` | `claude` 为什么询问：钩子的文本、安全检查的说明……类型为 `hook`、`safetyCheck`、`subcommandResults`、`other`…… |
| `default_to_no` | `claude` 要求默认选中“否” |
| `suppress_always_allow_rule` | `claude` 不提供可保存的规则：没有“始终允许” |
| `mcp_server.source` | 工具的 MCP 服务器来自哪里；claude.ai 连接器为 `claudeai` |

录制中出现了 `Edit`、`Write`、`Bash`、`PowerShell`、`ExitPlanMode` 和 `AskUserQuestion` 的请求。子智能体
的请求带有 `agent_id`（`subagent.stream.ndjson` 第 50 行）；钩子的请求带有
`decision_reason_type: "hook"`（`hook-ask` 第 20 行）；受保护文件夹的请求带有
`decision_reason_type: "safetyCheck"`（`seguranca` 第 20 行）。

### 权限回复

三种决定，用带该请求 `request_id` 的 `success` 类型 `control_response` 回复：

**允许**：`input` 原样放回 `updatedInput`。

<!-- stdin -->
```json
{"type":"control_response","response":{"subtype":"success","request_id":"0f8155c2-5135-4e0b-9da0-4cbc9d41ee1e","response":{"behavior":"allow","updatedInput":{"file_path":"C:\\demo\\app\\notes.md","old_string":"line three\n","new_string":"line 3 (edited)\n","replace_all":false}}}}
```

**始终允许**：同上，再把请求中的 `permission_suggestions`（如果是列表）放进 `updatedPermissions`。

<!-- stdin -->
```json
{"type":"control_response","response":{"subtype":"success","request_id":"…","response":{"behavior":"allow","updatedInput":{"…":"…"},"updatedPermissions":[{"type":"setMode","mode":"acceptEdits","destination":"session"}]}}}
```

**拒绝**：附带给模型的消息。没有消息时，库发送 `"The user denied this tool use."`。

<!-- stdin -->
```json
{"type":"control_response","response":{"subtype":"success","request_id":"…","response":{"behavior":"deny","message":"Do not create files, just explain."}}}
```

这条消息以 `is_error: true` 的 `tool_result` 文本到达模型。在 `denied` 场景中，模型读到消息后在
`result` 中说明它没有创建文件。

:::note[退出计划模式]
计划的结束是一个 `ExitPlanMode` 的权限请求。要退出计划模式并按计划继续，录制器先发送
`set_permission_mode` 切换到下一个模式（`plan` 场景中为 `acceptEdits`，`plan-auto` 中为 `auto`），然后
允许。附带消息拒绝，则会把计划连同这条消息退回给模型（`plan-feedback` 场景）。
:::

### 权限回复的规则

> 对权限请求的回复，始终带有进程一侧自己从 `claude` 收到的 `input` 和 `permission_suggestions`。
> 调用方只选择 `request_id`、决定和消息。

这是库的规则，而不是 `claude` 的规则：它会接受收到的任何 `updatedInput`，这个字段本来就是为此而设。
但持有某个 `claude` 的 stdin 的一方，不应执行来自别处的 `input`。如果请求经过界面、网络或另一个进程，
回复的一方可能把 `"command": "ls"` 换成别的命令，而 `claude` 会以对第一个命令的许可去执行被换掉的命令。

在库中：

- **回复命令（`AnswerPermission`）没有 `input` 字段**，也没有建议字段：只有 id、决定和消息；
- **写入 stdin 的一方用它自己从 stdout 读到的请求来组装回复**，这些请求保存在请求簿
  （`PermissionLedger`）中；
- **每个请求只回复一次。** 回复会把请求从簿中取出。`claude` 取消请求、该调用的 `tool_result` 到达、
  或这一轮对话结束时，请求也会被移出。不在待处理状态的 id（已回复、已取消、已解决或未知）不会写出任何
  内容。

从 2.1.321 起，`AskUserQuestion` 会拒绝修改了 `input` 的宿主的回复（字段 `afkTimeoutMs`）。遵守这条规则，
就不会出现这种情况。

:::caution[尚未核对]
使用 `--permission-prompt-tool` 时，较大的 "allow" 结果会被 MCP 的大输出处理写入文件
（[#109249](https://github.com/anthropics/claude-code/issues/109249)）。回复会把整个 `input` 发回，所以写
大文件的 `Write` 会经过这里。还需要用真实的 `claude` 验证。
:::

### `control_cancel_request`

`claude` 放弃一个尚未得到回复的权限请求：

<!-- fixture: interrupted.stream.ndjson:21 -->
```json
{"type": "control_cancel_request", "request_id": "dbfbeade-6899-46b2-ba43-120f8dea7acd"}
```

在 `interrupted` 场景中，第 20 行的 `PowerShell` 请求在第 21 行被 interrupt 取消。取消之后，请求从簿中
移出，对它的回复不会写出任何内容。

### 无人处理的请求

`claude` 还可能发送其他类型的控制请求：SDK 钩子的回调、发给 SDK MCP 服务器的消息、elicitation。没有回复
的话，它会一直等待。库的传输层对其中任何一种都以错误回复：

<!-- stdin -->
```json
{"type":"control_response","response":{"subtype":"error","request_id":"…","error":"not supported"}}
```

而读取器无法读取的 `can_use_tool`（例如没有 `tool_name`），因为永远到不了回复的一方，会收到错误
`"malformed permission request"`。
