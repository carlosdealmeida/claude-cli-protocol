---
title: 事件
description: 讀取器產生的事件一覽——每個事件來自哪一行、有哪些欄位，以及哪些沒有任何錄製涵蓋。
---

:::note
本頁由 AI 翻譯，尚未經母語人士審閱。術語見 [docs/i18n/glossary.md](https://github.com/carlosdealmeida/claude-cli-protocol/blob/master/docs/i18n/glossary.md)。
:::

讀取器把每一行轉換為零個、一個或多個有型別的事件。本頁是它們的參考：名稱、來源行與欄位。**名稱採用預期
結果格式中的名稱**（見 [Fixtures](../fixtures/#預期結果)），使用 `snake_case`，而不是串流中的名稱：
`system/init` 行的 `cwd` 是 `session_started` 的 `working_directory`。其他語言的程式庫可以為自己的型別取別的
名稱，但它產生的預期結果使用這些名稱。

適用於所有事件的規則：

- **欄位集合是固定的。** 沒有值的欄位為 `null`；它不會消失。
- **原樣傳遞的值**（`tool_use` 與 `permission_requested` 的 `input`、`suggestions`、`tool_use_result`
  以及 `control_acknowledged` 的 `response`）依 `claude` 送出的原樣保留，使用它的鍵名。
- **即時讀取器與檔案讀取器。** 「stdout」指 `claude -p` 各行的讀取器；「對話記錄」指對話記錄（`.jsonl`）
  的讀取器。有些事件兩者都會產生。「來源」一欄說明這一行實際出現在哪裡：工作、掛鉤、`api_retry`、
  `thinking_tokens`、`commands_changed`、`bridge_state` 與 `local_command` 的 `system` 行由兩個讀取器的
  同一段程式碼讀取，對話記錄中有這些行時同樣會產生事件。`init`、`status`、`per_turn_effort_changed`、
  `model_refusal_fallback` 與 `peer_message_hold` 只出現在 stdout 中。

## 工作階段與一輪對話

| 事件 | 來源 | 欄位 |
|---|---|---|
| `session_started` | stdout：`system/init` | `session_id`, `model`, `working_directory`, `permission_mode`, `claude_version`, `terminal_commands`, `fast_mode_state`, `ultrathink_available` |
| `status_changed` | stdout：`system/status` | `status`, `permission_mode` |
| `turn_completed` | stdout：`result` | `subtype`, `is_error`, `duration_ms`, `cost_usd`, `input_tokens`, `output_tokens`, `api_duration_ms`, `error_message`, `fast_mode_state` |
| `system_notice` | stdout 與對話記錄：`system/compact_boundary`、`system/api_retry` | `subtype`, `trigger`, `pre_tokens`, `post_tokens`, `error_message` |
| `informational` | stdout：`system/informational` | `level`, `text` |
| `thinking_tokens` | stdout：`system/thinking_tokens` | `estimated_tokens` |
| `prompt_cache_used` | stdout 與對話記錄：主對話中 `assistant` 行的 `usage` | `ttl`, `at` |

- `turn_completed.input_tokens` 是 `input_tokens`、`cache_creation_input_tokens` 與
  `cache_read_input_tokens` 的總和。`cost_usd` 即 `total_cost_usd`。
- `prompt_cache_used.ttl` 為 1 小時或 5 分鐘，取決於 `cache_creation`（`ephemeral_1h_input_tokens` 或
  `ephemeral_5m_input_tokens`）；回覆只讀取了快取時為 `null`。
- `informational.level` 為 `info`、`warning` 或 `error`（行中未說明時為 `info`）。攔下提示的掛鉤會送出
  `warning`（`hook-blocked` 情境）。

## 正在抵達的回覆

| 事件 | 來源 | 欄位 |
|---|---|---|
| `message_started` | stdout：`stream_event` / `message_start` | `message_id`, `parent_tool_use_id` |
| `block_started` | stdout：`stream_event` / `content_block_start` | `message_id`, `index`, `parent_tool_use_id`, `block` |
| `block_delta` | stdout：`stream_event` / `content_block_delta` | `message_id`, `index`, `parent_tool_use_id`, `kind`, `text`, `estimated_tokens` |
| `block_stopped` | stdout：`stream_event` / `content_block_stop` | `message_id`, `index`, `parent_tool_use_id` |

- 後三者的 `message_id` 是同一 `parent_tool_use_id` 下最後一個 `message_start` 的 id；若還沒有，則為空。
- `block_delta.kind` 為 `text`、`thinking` 或 `input_json`。
- `tool_use` 的 `block_started` 中，`block` 的 `input` 為空（`{}`）。

## 訊息

| 事件 | 來源 | 欄位 |
|---|---|---|
| `assistant_block` | stdout 與對話記錄：`assistant`，每個區塊一個 | `message_id`, `parent_tool_use_id`, `block`, `timestamp`, `thinking_duration_ms`, `uuid`, `supersedes`, `is_synthetic` |
| `user_block` | stdout 與對話記錄：`user`（以及對話記錄中 `queued_command` 的 `attachment`） | `parent_tool_use_id`, `block`, `tool_use_result`, `is_meta`, `is_compact_summary`, `timestamp`, `images`, `is_synthetic`, `origin`, `uuid`, `is_queued_command`, `context` |
| `command_echo` | 對話記錄：`user` 行中的 `<command-name>` 文字 | `name`, `arguments` |
| `command_output` | stdout 與對話記錄：`<local-command-stdout>` 文字；`system/local_command` | `text` |

- 當 `message.model` 為 `<synthetic>` 時，`assistant_block.is_synthetic` 為真。
- `assistant_block.supersedes` 是這一行所取代的各行的 `uuid`（`claude` 捨棄後重新寫出的回覆）。
- `user_block.origin` 說明訊息來自哪裡：`kind`（`human`、`peer`、`channel`、`task-notification`、
  `coordinator`、`plugin`……）、`name`、`sender_task_id` 與 `subkind`。
- `user_block.context` 是 IDE 在文字之前送出的上下文區塊：終端機的輸出、瀏覽器分頁、瀏覽器指示或編輯器中的
  選取範圍。每個區塊有 `kind`（`terminal`、`browser-tab`、`browser-instructions` 或 `selection`）、
  `subject` 與 `content`。讀取器會把這些區塊從文字中取出，所以它們只出現在這裡。沒有任何錄製帶有這樣的區塊：
  在所有錄製中這份清單都是空的。
- `command_echo.name` 不含斜線。

### 區塊

`block` 欄位是一個帶有 `kind` 與該區塊欄位的物件：

| `kind` | 欄位 |
|---|---|
| `text` | `text` |
| `thinking` | `text` |
| `tool_use` | `id`, `name`, `input` |
| `tool_result` | `tool_use_id`, `text`, `is_error`, `non_execution_kind`, `images` |
| `image` | `media_type`, `data` |
| `advisor_use` | `id` |
| `advisor_result` | `tool_use_id`, `outcome`, `text`, `error_code` |

`tool_result` 的 `text` 以 `\n` 串接 `content` 中的文字區塊。圖片的 `data` 在預期結果中寫作
`{"length": …, "sha256": …}`，而不是完整內容。

最後兩種屬於 advisor，它是一個伺服器端工具（情境 `advisor`）。`advisor_use` 是呼叫：一個名為 `advisor` 的
`server_tool_use` 區塊。`advisor_result` 是評審結論，也就是 `advisor_tool_result` 區塊，它出現在助理的訊息中，
而不是 `user` 行中。`outcome` 為 `reviewed`、`declined`（結論的 `stop_reason` 為 `refusal`）或
`unavailable`（帶 `error_code`）。當結論是加密的或沒有傳回時，`text` 為 `null`。

## 權限與控制

| 事件 | 來源 | 欄位 |
|---|---|---|
| `permission_requested` | stdout：`control_request` / `can_use_tool` | `request_id`, `tool_name`, `display_name`, `description`, `tool_use_id`, `input`, `suggestions`, `agent_id`, `default_to_no`, `suppress_always_allow_rule`, `decision_reason`, `decision_reason_type`, `decision_reason_code`, `mcp_server_source` |
| `permission_cancelled` | stdout：`control_cancel_request` | `request_id` |
| `control_acknowledged` | stdout：`control_response` | `request_id`, `is_error`, `response`, `error` |
| `commands_available` | stdout：帶有 `commands` 的 `control_response` 內容；`system/commands_changed` | `commands` |
| `models_available` | stdout：帶有 `models` 的 `control_response` 內容 | `models` |
| `auth_conflict_reported` | stdout：帶有 `commands`，且帶有 `models` 或 `account` 的 `control_response` 內容 | `conflict` |
| `settings_applied` | stdout：帶有 `applied` 的 `control_response` 內容 | `model`, `effort`, `has_effort`, `ultrathink_available` |
| `context_usage_reported` | stdout：帶有 `categories` 與 `totalTokens` 的 `control_response` 內容 | `usage` |

- 一個 `control_response` 先產生一個 `control_acknowledged`，接著依[控制](../controle/#如何讀取回覆內容)
  中表格的順序產生內容所含的事件。
- `commands[]`：`name`、`description`、`argument_hint`。
- `models[]`：`value`、`resolved_model`、`display_name`、`description`、`effort_levels`。
- `usage`：`total_tokens`、`max_tokens`、`percentage`（四捨五入的整數）、`categories[]`（`name`、`tokens`、
  `kind`）、`model`、`memory_files[]`（`path`、`tokens`）與 `agents[]`（`name`、`tokens`）。
- `conflict`：`token_source` 與 `api_key_source`；沒有衝突時為 `null`。
- `applied` 中沒有 `effort` 時，`settings_applied.has_effort` 為假。

## 背景工作

| 事件 | 來源 | 欄位 |
|---|---|---|
| `agent_task_started` | stdout：`system/task_started` | `task_id`, `tool_use_id`, `description`, `agent_type`, `is_background`, `depth`, `task_type`, `prompt` |
| `agent_task_progress` | stdout：`system/task_progress` | `task_id`, `tool_use_id`, `activity`, `tokens`, `tool_uses`, `duration_ms` |
| `agent_task_updated` | stdout：`system/task_updated` | `task_id`, `status`, `is_backgrounded` |
| `agent_task_finished` | stdout：`system/task_notification`；對話記錄：`user` 行中的 `<task-notification>` 文字 | `task_id`, `tool_use_id`, `status`, `summary`, `tokens`, `tool_uses`, `duration_ms` |
| `background_tasks_changed` | stdout: `system/background_tasks_changed` | `tasks` |

- `task_type` 對子智慧體為 `local_agent`，對命令為 `local_bash`，對 workflow 為 `local_workflow`。
- `depth` 即 `spawn_depth`（行中未說明時為 1）。
- `tokens`、`tool_uses` 與 `duration_ms` 來自該行的 `usage`。
- `background_tasks_changed.tasks[]`：`task_id`、`task_type` 與 `description`。每一行都帶來完整的清單，
  並取代上一行的清單；空清單表示沒有工作在執行。沒有 `task_id` 的項目會被略過。工作階段檔案中從不出現這一行。

## 沒有任何錄製涵蓋的事件

這些事件來自 50 份錄製中都沒有的行。它們的結構以 .NET 程式庫的程式碼與單元測試為準，也就是這裡所寫的內容。
當某份錄製包含它們時，會以它核對本表。

| 事件 | 來源 | 欄位 |
|---|---|---|
| `hook_started` | stdout 與對話記錄：`system/hook_started` | `hook_id`, `hook_event` |
| `hook_finished` | stdout 與對話記錄：`system/hook_response` | `hook_id` |
| `command_lifecycle` | stdout：`command_lifecycle` | `command_uuid`, `state`, `ran_ahead` |
| `per_turn_effort_changed` | stdout：`system/per_turn_effort_changed` | 無 |
| `usage_limits_reported` | stdout：`rate_limit_event`；帶有 `rate_limits` 的 `control_response` 內容 | `limits` |
| `account_reported` | stdout：帶有 `account` 的 `control_response` 內容 | `account` |
| `remote_control_changed` | stdout 與對話記錄：`system/bridge_state`；帶有 `session_url` 的 `control_response` 內容 | `state`, `url`, `detail` |
| `model_refusal_fallback` | stdout：`system/model_refusal_fallback` | `original_model`, `fallback_model`, `category` |
| `peer_message_hold` | stdout：`system/peer_message_hold` | `state`, `from`, `message_uuid`, `from_name`, `cause`, `outcome` |

- `hook_started.hook_event` 是掛鉤所回應的事件：`SessionStart`、`Setup`、`PreToolUse`……
- `command_lifecycle.state` 為 `queued`、`started`、`completed`、`cancelled`、`discarded` 或 `refused`
  （見[命令](../comandos/#帶-uuid)）。
- `per_turn_effort_changed` 表示提示中的 `ultrathink` 一詞提高了這一輪的 effort。
- `limits`：`windows[]`（`name`、`utilization`、`resets_at`、`model`）與 `is_snapshot`。`rate_limit_event`
  只帶有變動的視窗，`utilization` 介於 0 到 1，`resetsAt` 為 Unix 秒；`get_usage` 的回覆是一份完整快照
  （`is_snapshot` 為真）。
- `account`：`auth_method`（`claudeai`、`api-key`、`bedrock`、`vertex`、`foundry`、`gateway` 或
  `third-party`）、`email`、`organization` 與 `plan`（方案名稱，例如 "Max"）。
- `model_refusal_fallback`：`claude` 的防護機制切換了模型，回覆來自 `fallback_model`。
- `peer_message_hold.state` 為 `held`、`released` 或 `dropped`：來自另一個工作階段、被 `claude` 暫扣等待
  使用者審閱的訊息。

## 工作階段的事件

| 事件 | 來源 | 欄位 |
|---|---|---|
| `control_replied` | `ClaudeSession`，而不是讀取器：對它送出的請求的回覆 | `command`, `reply` |

它以 `request_id` 把工作階段送出的請求（`command`）和回覆它的 `control_acknowledged`（`reply`）配成一對。
它不會出現在預期結果中，因為它不是來自某一行。

## 不產生事件的行

- `message_delta` 與 `message_stop` 的 `stream_event`；
- `system/model_refusal_no_fallback`；
- 任何不是 `can_use_tool` 的 `control_request`（傳輸層以錯誤回覆，見
  [控制](../controle/#無人處理的請求)）；
- 所有 `type` 或 `subtype` 未知的行。
