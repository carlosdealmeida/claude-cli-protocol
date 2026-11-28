---
title: 事件
description: 读取器产生的事件一览——每个事件来自哪一行、有哪些字段，以及哪些没有任何录制覆盖。
---

:::note
本页由 AI 翻译，尚未经母语者审校。术语见 [docs/i18n/glossary.md](https://github.com/carlosdealmeida/claude-cli-protocol/blob/master/docs/i18n/glossary.md)。
:::

读取器把每一行转换为零个、一个或多个有类型的事件。本页是它们的参考：名称、来源行和字段。**名称采用预期
结果格式中的名称**（见 [Fixtures](../fixtures/#预期结果)），使用 `snake_case`，而不是流中的名称：
`system/init` 行的 `cwd` 是 `session_started` 的 `working_directory`。其他语言的库可以给自己的类型起别的
名字，但它产生的预期结果使用这些名称。

适用于所有事件的规则：

- **字段集合是固定的。** 没有值的字段为 `null`；它不会消失。
- **原样传递的值**（`tool_use` 和 `permission_requested` 的 `input`、`suggestions`、`tool_use_result`
  以及 `control_acknowledged` 的 `response`）按 `claude` 发送的原样保留，使用它的键名。
- **实时读取器和文件读取器。** “stdout”指 `claude -p` 各行的读取器；“对话记录”指对话记录（`.jsonl`）
  的读取器。有些事件两者都会产生。“来源”一列说明这一行实际出现在哪里：任务、钩子、`api_retry`、
  `thinking_tokens`、`commands_changed`、`bridge_state` 和 `local_command` 的 `system` 行由两个读取器的
  同一段代码读取，对话记录中有这些行时同样会产生事件。`init`、`status`、`per_turn_effort_changed`、
  `model_refusal_fallback` 和 `peer_message_hold` 只出现在 stdout 中。

## 会话与一轮对话

| 事件 | 来源 | 字段 |
|---|---|---|
| `session_started` | stdout：`system/init` | `session_id`, `model`, `working_directory`, `permission_mode`, `claude_version`, `terminal_commands`, `fast_mode_state`, `ultrathink_available` |
| `status_changed` | stdout：`system/status` | `status`, `permission_mode` |
| `turn_completed` | stdout：`result` | `subtype`, `is_error`, `duration_ms`, `cost_usd`, `input_tokens`, `output_tokens`, `api_duration_ms`, `error_message`, `fast_mode_state` |
| `system_notice` | stdout 和对话记录：`system/compact_boundary`、`system/api_retry` | `subtype`, `trigger`, `pre_tokens`, `post_tokens`, `error_message` |
| `informational` | stdout：`system/informational` | `level`, `text` |
| `thinking_tokens` | stdout：`system/thinking_tokens` | `estimated_tokens` |
| `prompt_cache_used` | stdout 和对话记录：主对话中 `assistant` 行的 `usage` | `ttl`, `at` |

- `turn_completed.input_tokens` 是 `input_tokens`、`cache_creation_input_tokens` 和
  `cache_read_input_tokens` 之和。`cost_usd` 即 `total_cost_usd`。
- `prompt_cache_used.ttl` 为 1 小时或 5 分钟，取决于 `cache_creation`（`ephemeral_1h_input_tokens` 或
  `ephemeral_5m_input_tokens`）；回复只读取了缓存时为 `null`。
- `informational.level` 为 `info`、`warning` 或 `error`（行中未说明时为 `info`）。拦截了提示的钩子会发送
  `warning`（`hook-blocked` 场景）。

## 正在到达的回复

| 事件 | 来源 | 字段 |
|---|---|---|
| `message_started` | stdout：`stream_event` / `message_start` | `message_id`, `parent_tool_use_id` |
| `block_started` | stdout：`stream_event` / `content_block_start` | `message_id`, `index`, `parent_tool_use_id`, `block` |
| `block_delta` | stdout：`stream_event` / `content_block_delta` | `message_id`, `index`, `parent_tool_use_id`, `kind`, `text`, `estimated_tokens` |
| `block_stopped` | stdout：`stream_event` / `content_block_stop` | `message_id`, `index`, `parent_tool_use_id` |

- 后三者的 `message_id` 是同一 `parent_tool_use_id` 下最后一个 `message_start` 的 id；如果还没有，则为空。
- `block_delta.kind` 为 `text`、`thinking` 或 `input_json`。
- `tool_use` 的 `block_started` 中，`block` 的 `input` 为空（`{}`）。

## 消息

| 事件 | 来源 | 字段 |
|---|---|---|
| `assistant_block` | stdout 和对话记录：`assistant`，每个块一个 | `message_id`, `parent_tool_use_id`, `block`, `timestamp`, `thinking_duration_ms`, `uuid`, `supersedes`, `is_synthetic` |
| `user_block` | stdout 和对话记录：`user`（以及对话记录中 `queued_command` 的 `attachment`） | `parent_tool_use_id`, `block`, `tool_use_result`, `is_meta`, `is_compact_summary`, `timestamp`, `images`, `is_synthetic`, `origin`, `uuid`, `is_queued_command`, `context` |
| `command_echo` | 对话记录：`user` 行中的 `<command-name>` 文本 | `name`, `arguments` |
| `command_output` | stdout 和对话记录：`<local-command-stdout>` 文本；`system/local_command` | `text` |

- 当 `message.model` 为 `<synthetic>` 时，`assistant_block.is_synthetic` 为真。
- `assistant_block.supersedes` 是这一行所替换的各行的 `uuid`（`claude` 丢弃后重新写出的回复）。
- `user_block.origin` 说明消息来自哪里：`kind`（`human`、`peer`、`channel`、`task-notification`、
  `coordinator`、`plugin`……）、`name`、`sender_task_id` 和 `subkind`。
- `user_block.context` 是 IDE 在文本之前发送的上下文块：终端的输出、浏览器标签页、浏览器指令或编辑器中的
  选区。每个块有 `kind`（`terminal`、`browser-tab`、`browser-instructions` 或 `selection`）、`subject`
  和 `content`。读取器会把这些块从文本中取出，所以它们只出现在这里。没有任何录制带有这样的块：在所有录制中
  这个列表都是空的。
- `command_echo.name` 不带斜杠。

### 块

`block` 字段是一个带有 `kind` 和该块字段的对象：

| `kind` | 字段 |
|---|---|
| `text` | `text` |
| `thinking` | `text` |
| `tool_use` | `id`, `name`, `input` |
| `tool_result` | `tool_use_id`, `text`, `is_error`, `non_execution_kind`, `images` |
| `image` | `media_type`, `data` |
| `advisor_use` | `id` |
| `advisor_result` | `tool_use_id`, `outcome`, `text`, `error_code` |

`tool_result` 的 `text` 用 `\n` 连接 `content` 中的文本块。图片的 `data` 在预期结果中写作
`{"length": …, "sha256": …}`，而不是完整内容。

最后两种属于 advisor，它是一个服务器端工具（场景 `advisor`）。`advisor_use` 是调用：一个名为 `advisor` 的
`server_tool_use` 块。`advisor_result` 是评审结论，即 `advisor_tool_result` 块，它出现在助手的消息中，而不是
`user` 行中。`outcome` 为 `reviewed`、`declined`（结论的 `stop_reason` 为 `refusal`）或 `unavailable`
（带 `error_code`）。当结论是加密的或没有返回时，`text` 为 `null`。

## 权限与控制

| 事件 | 来源 | 字段 |
|---|---|---|
| `permission_requested` | stdout：`control_request` / `can_use_tool` | `request_id`, `tool_name`, `display_name`, `description`, `tool_use_id`, `input`, `suggestions`, `agent_id`, `default_to_no`, `suppress_always_allow_rule`, `decision_reason`, `decision_reason_type`, `decision_reason_code`, `mcp_server_source` |
| `permission_cancelled` | stdout：`control_cancel_request` | `request_id` |
| `control_acknowledged` | stdout：`control_response` | `request_id`, `is_error`, `response`, `error` |
| `commands_available` | stdout：带 `commands` 的 `control_response` 正文；`system/commands_changed` | `commands` |
| `models_available` | stdout：带 `models` 的 `control_response` 正文 | `models` |
| `auth_conflict_reported` | stdout：带 `commands`，且带 `models` 或 `account` 的 `control_response` 正文 | `conflict` |
| `settings_applied` | stdout：带 `applied` 的 `control_response` 正文 | `model`, `effort`, `has_effort`, `ultrathink_available` |
| `context_usage_reported` | stdout：带 `categories` 和 `totalTokens` 的 `control_response` 正文 | `usage` |

- 一个 `control_response` 先产生一个 `control_acknowledged`，然后按[控制](../controle/#如何读取响应体)
  中表格的顺序产生正文中包含的事件。
- `commands[]`：`name`、`description`、`argument_hint`。
- `models[]`：`value`、`resolved_model`、`display_name`、`description`、`effort_levels`。
- `usage`：`total_tokens`、`max_tokens`、`percentage`（四舍五入的整数）、`categories[]`（`name`、
  `tokens`、`kind`）、`model`、`memory_files[]`（`path`、`tokens`）和 `agents[]`（`name`、`tokens`）。
- `conflict`：`token_source` 和 `api_key_source`；没有冲突时为 `null`。
- `applied` 中没有 `effort` 时，`settings_applied.has_effort` 为假。

## 后台任务

| 事件 | 来源 | 字段 |
|---|---|---|
| `agent_task_started` | stdout：`system/task_started` | `task_id`, `tool_use_id`, `description`, `agent_type`, `is_background`, `depth`, `task_type`, `prompt` |
| `agent_task_progress` | stdout：`system/task_progress` | `task_id`, `tool_use_id`, `activity`, `tokens`, `tool_uses`, `duration_ms` |
| `agent_task_updated` | stdout：`system/task_updated` | `task_id`, `status`, `is_backgrounded` |
| `agent_task_finished` | stdout：`system/task_notification`；对话记录：`user` 行中的 `<task-notification>` 文本 | `task_id`, `tool_use_id`, `status`, `summary`, `tokens`, `tool_uses`, `duration_ms` |
| `background_tasks_changed` | stdout: `system/background_tasks_changed` | `tasks` |

- `task_type` 对子智能体为 `local_agent`，对命令为 `local_bash`，对 workflow 为 `local_workflow`。
- `depth` 即 `spawn_depth`（行中未说明时为 1）。
- `tokens`、`tool_uses` 和 `duration_ms` 来自该行的 `usage`。
- `background_tasks_changed.tasks[]`：`task_id`、`task_type` 和 `description`。每一行都带来完整的列表，
  并替换上一行的列表；空列表表示没有任务在运行。没有 `task_id` 的条目会被跳过。会话文件中从不出现这一行。

## 没有任何录制覆盖的事件

这些事件来自 50 份录制中都没有的行。它们的结构以 .NET 库的代码和单元测试为准，即这里写的内容。当某份
录制包含它们时，会据此核对本表。

| 事件 | 来源 | 字段 |
|---|---|---|
| `hook_started` | stdout 和对话记录：`system/hook_started` | `hook_id`, `hook_event` |
| `hook_finished` | stdout 和对话记录：`system/hook_response` | `hook_id` |
| `command_lifecycle` | stdout：`command_lifecycle` | `command_uuid`, `state`, `ran_ahead` |
| `per_turn_effort_changed` | stdout：`system/per_turn_effort_changed` | 无 |
| `usage_limits_reported` | stdout：`rate_limit_event`；带 `rate_limits` 的 `control_response` 正文 | `limits` |
| `account_reported` | stdout：带 `account` 的 `control_response` 正文 | `account` |
| `remote_control_changed` | stdout 和对话记录：`system/bridge_state`；带 `session_url` 的 `control_response` 正文 | `state`, `url`, `detail` |
| `model_refusal_fallback` | stdout：`system/model_refusal_fallback` | `original_model`, `fallback_model`, `category` |
| `peer_message_hold` | stdout：`system/peer_message_hold` | `state`, `from`, `message_uuid`, `from_name`, `cause`, `outcome` |

- `hook_started.hook_event` 是钩子所响应的事件：`SessionStart`、`Setup`、`PreToolUse`……
- `command_lifecycle.state` 为 `queued`、`started`、`completed`、`cancelled`、`discarded` 或 `refused`
  （见[命令](../comandos/#带-uuid)）。
- `per_turn_effort_changed` 表示提示中的 `ultrathink` 一词提高了这一轮的 effort。
- `limits`：`windows[]`（`name`、`utilization`、`resets_at`、`model`）和 `is_snapshot`。
  `rate_limit_event` 只带有发生变化的窗口，`utilization` 为 0 到 1，`resetsAt` 为 Unix 秒；`get_usage`
  的回复是一份完整快照（`is_snapshot` 为真）。
- `account`：`auth_method`（`claudeai`、`api-key`、`bedrock`、`vertex`、`foundry`、`gateway` 或
  `third-party`）、`email`、`organization` 和 `plan`（套餐名称，例如 "Max"）。
- `model_refusal_fallback`：`claude` 的安全机制切换了模型，回复来自 `fallback_model`。
- `peer_message_hold.state` 为 `held`、`released` 或 `dropped`：来自另一个会话、被 `claude` 暂扣等待用户
  审阅的消息。

## 会话的事件

| 事件 | 来源 | 字段 |
|---|---|---|
| `control_replied` | `ClaudeSession`，而不是读取器：对它发出的请求的回复 | `command`, `reply` |

它按 `request_id` 把会话发出的请求（`command`）和回复它的 `control_acknowledged`（`reply`）配成一对。它不
出现在预期结果中，因为它不来自某一行。

## 不产生事件的行

- `message_delta` 和 `message_stop` 的 `stream_event`；
- `system/model_refusal_no_fallback`；
- 任何不是 `can_use_tool` 的 `control_request`（传输层以错误回复，见
  [控制](../controle/#无人处理的请求)）；
- 所有 `type` 或 `subtype` 未知的行。
