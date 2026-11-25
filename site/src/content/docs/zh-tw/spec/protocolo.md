---
title: 通訊協定
description: 如何呼叫 claude -p、一行的格式、stdout 中各類行，以及一輪對話。
---

:::note
本頁由 AI 翻譯，尚未經母語人士審閱。術語見 [docs/i18n/glossary.md](https://github.com/carlosdealmeida/claude-cli-protocol/blob/master/docs/i18n/glossary.md)。
:::

加上 `--input-format stream-json` 和 `--output-format stream-json` 的 `claude -p` 是一個處理程序：它在
stdin 上每個命令讀取一行 JSON，在 stdout 上每個事件寫出一行 JSON。本頁說明 stdout 上會輸出什麼。寫入
stdin 的內容見[命令](../comandos/)與[控制](../controle/)；處理程序本身見[傳輸層](../transporte/)。

:::caution[這個通訊協定沒有契約]
Anthropic 並未將這個通訊協定記載為穩定的介面
（[anthropics/claude-code#111046](https://github.com/anthropics/claude-code/issues/111046)）。這裡的內容
是對照 `fixtures/` 中的錄製（以 Claude Code 2.1.331 錄製，`image` 情境為 2.1.286）以及 .NET 程式庫的
程式碼核對過的。新版本可能改變任何一行。每週重新錄製 fixture 就是用來提出警示的。
:::

## 呼叫

```
claude -p --input-format stream-json --output-format stream-json --verbose
       --include-partial-messages --permission-prompt-tool stdio --permission-mode <modo>
```

- `--verbose` 是必要的：少了它，`claude -p` 會拒絕 `--output-format stream-json`。
- `--include-partial-messages` 讓它在回覆抵達時分段寫出（也就是 `stream_event` 行）。
- `--permission-prompt-tool stdio` 讓它在自己的 stdout 上以 `can_use_tool` 的 `control_request`
  請求權限，並在 stdin 上等待回覆。

選用參數（`--resume`、`--model`、`--effort`）以及每個值的檢查見[傳輸層](../transporte/#參數)。

## 行

- **一行一個 JSON 物件**，以 `\n` 結尾。stdout 為 UTF-8。
- **`type` 欄位**說明這一行是什麼。在 `system` 中，`subtype` 說明是哪種通知；在 `stream_event` 中是
  `event.type`；在 `control_request` 中是 `request.subtype`。
- **幾乎每一行都帶有 `session_id` 和 `uuid`。** `session_id` 屬於工作階段；`uuid` 屬於這一行。
- **讀取器不會擲回例外。** 空行、不以 `{` 開頭的行（去掉開頭的空白與終端機控制碼之後）、無效的 JSON、
  未知的 `type` 或缺少的欄位：這一行不產生事件，讀取繼續進行。.NET 程式庫的兩個讀取器都是這麼做的，
  這也讓 `claude` 新增一種行時，程式庫仍能正常運作。
- **未知的欄位會被忽略。** 每一行帶的欄位遠多於讀取器用到的（`result` 有二十多個）。讀取器只讀它認得的。

## 行的類型

在 21 份 stdout 錄製（`fixtures/*.stream.ndjson`）中的數量：

| `type` | 子類型 | 錄製中出現次數 | 意義 |
|---|---|---:|---|
| `system` | `init` | 28 | 一輪對話的開始：工作階段、模型、資料夾、模式、版本 |
| `system` | `status` | 66 | `claude` 正在做什麼（`requesting`、`compacting`、`null`）以及模式 |
| `system` | `thinking_tokens` | 28 | `claude` 在正在抵達的區塊中到目前為止思考了多少權杖 |
| `system` | `task_started`、`task_progress`、`task_updated`、`task_notification` | 6 | 一個背景工作：子智慧體或命令 |
| `system` | `compact_boundary` | 2 | 對話被壓縮了 |
| `system` | `informational` | 1 | `claude` 對自身工作的通知（例如某個掛鉤攔下了提示） |
| `system` | `background_tasks_changed` | 2 | 背景工作清單有變化。程式庫不讀取 |
| `stream_event` | `message_start` | 53 | 一則回覆開始抵達 |
| `stream_event` | `content_block_start`、`content_block_delta`、`content_block_stop` | 1,015 | 回覆的各個區塊，一段一段地 |
| `stream_event` | `message_delta`、`message_stop` | 106 | 回覆結束。程式庫不讀取 |
| `assistant` | | 86 | 回覆中一個完整的區塊 |
| `user` | | 45 | 回到模型的內容：工具結果、`claude` 自己寫的文字 |
| `control_request` | `can_use_tool` | 19 | 一個權限請求 |
| `control_cancel_request` | | 1 | `claude` 放棄了一個權限請求 |
| `control_response` | `success` | 36 | 對宿主發出的控制請求的回覆 |
| `result` | `success`、`error_during_execution` | 28 | 一輪對話的結束 |

有些行程式庫會讀取，但沒有任何錄製包含。在 `system` 中：`api_retry`、`hook_started`、`hook_response`、
`commands_changed`、`bridge_state`、`local_command`、`per_turn_effort_changed`、
`model_refusal_fallback` 與 `peer_message_hold`。`system` 之外：`rate_limit_event`（錄製器會將它移除，
因為它屬於錄製者的帳戶）和 `command_lifecycle`。它們產生的事件見[事件](../eventos/)，並標示為未錄製。

## 一輪對話的開始：`system/init`

`system/init` 行開啟的是每一輪對話，而不是每個處理程序。在 21 份錄製中，`system/init` 和 `result`
一對一交替出現。

<!-- fixture: edit.stream.ndjson:2 -->
```json
{"type": "system", "subtype": "init", "session_id": "8f8ab1b3-6d82-4572-9c2c-6f5f166d7bd2", "model": "claude-haiku-5-5", "cwd": "C:\\demo\\app", "permissionMode": "default", "claude_code_version": "2.1.331", "tools": ["Task", "AskUserQuestion", "Bash", "…"]}
```

讀取器從中取出：`session_id`、`model`、`cwd`、`permissionMode` 與 `claude_code_version`；有的話，還有
`terminal_slash_commands`、`fast_mode_state` 與 `ultrathink_available`。錄製只保留這一行中已知的欄位：
其餘的由錄製器移除（見 [Fixtures](../fixtures/#去識別化)）。

:::note
生效的模式是這一行中的模式，而不是命令列上要求的模式。自 2.1.323 起，當 auto 無法使用時，
`--permission-mode auto` 會從 `default` 開始
（[#111343](https://github.com/anthropics/claude-code/issues/111343)）。
:::

## 正在抵達的回覆：`stream_event`

加上 `--include-partial-messages` 後，模型的每則回覆會先以 Anthropic API 的串流格式分段抵達，放在
`event` 裡：

<!-- fixture: edit.stream.ndjson:5 -->
```json
{"type": "stream_event", "event": {"type": "content_block_start", "index": 0, "content_block": {"type": "tool_use", "id": "toolu_015zZi1E7vQS17Zr1yMU1XbP", "name": "Read", "input": {}}}, "parent_tool_use_id": null}
```

<!-- fixture: edit.stream.ndjson:7 -->
```json
{"type": "stream_event", "event": {"type": "content_block_delta", "index": 0, "delta": {"type": "input_json_delta", "partial_json": "{\"file_"}}, "parent_tool_use_id": null}
```

- **`message_start`** 帶有 `event.message.id`，也就是回覆的 id。之後的 delta 不帶這個 id：**讀取器為每個
  `parent_tool_use_id` 記住最後一個 `message_start` 的 id**，並把它放到各區塊的事件上。所以 stdout 讀取器
  有狀態，而對話記錄的讀取器沒有。
- **`content_block_start`** 開啟第 `index` 個區塊。`tool_use` 以空的 `input`（`{}`）開始。
- **`content_block_delta`** 延伸這個區塊。`delta.type` 說明是哪個部分：`text_delta`（欄位 `text`）、
  `thinking_delta`（欄位 `thinking`，有時還有 `estimated_tokens`）或 `input_json_delta`（欄位
  `partial_json`，是 `input` 的 JSON 的一段，要到最後才會閉合）。
- **`content_block_stop`** 關閉這個區塊。
- **`message_delta`** 和 **`message_stop`** 結束這則回覆。程式庫不讀取它們：`assistant` 行帶有完整的區塊。
- **`parent_tool_use_id`** 是寫出這則回覆的子智慧體所對應的工具呼叫；主對話為 `null`。

## 完整的區塊：`assistant`

分段之後，`claude` 以 API 的訊息格式把完整的回覆寫成 `assistant` 行：`message.id`、`message.content`
（區塊清單）和 `message.usage`。在錄製中，每個 `assistant` 行只有一個區塊，有三個區塊的回覆會變成三行，
`message.id` 相同。

<!-- fixture: edit.stream.ndjson:15 -->
```json
{"type": "assistant", "message": {"id": "msg_011ChNjwF76zsXQUUbNF7yfr", "role": "assistant", "content": [{"type": "tool_use", "id": "toolu_015zZi1E7vQS17Zr1yMU1XbP", "name": "Read", "input": {"file_path": "C:\\demo\\app\\notes.md"}}]}, "parent_tool_use_id": null}
```

讀取器認得的區塊：`text`、`thinking`、`tool_use`、`tool_result` 與 `image`。`message.model` 等於
`<synthetic>` 表示這一行是 `claude` 自己寫的，而不是模型寫的。

## 回到模型的內容：`user`

工具結果透過 `user` 行回來，`message.content` 中是一個 `tool_result` 區塊，`tool_use_result` 中是工具的
結構化結果：

<!-- fixture: edit.stream.ndjson:19 -->
```json
{"type": "user", "message": {"role": "user", "content": [{"tool_use_id": "toolu_015zZi1E7vQS17Zr1yMU1XbP", "type": "tool_result", "content": "1\tline one\n…"}]}, "parent_tool_use_id": null, "tool_use_result": {"type": "text", "file": {"filePath": "C:\\demo\\app\\notes.md"}}, "tool_result_meta": [{"id": "toolu_015zZi1E7vQS17Zr1yMU1XbP", "permission_decision": {"decision": "accept", "source": "config", "reason_type": "mode"}}]}
```

- `tool_result` 的 `content` 是文字或區塊清單（文字與圖片）。讀取器以 `\n` 串接文字，圖片另外保存。
- `is_error: true` 表示錯誤結果。工具沒有執行時，`tool_result_meta[].non_execution_kind` 說明原因：
  `permission-rule`（被拒絕）或 `user-rejected`（被中斷）。
- `claude` 也會在 `user` 行中寫入自己的文字：斜線命令的標記（`<local-command-stdout>`）、壓縮後的摘要、
  `[Request interrupted by user for tool use]`。宿主送出的訊息不會以 `user` 行回到 stdout（只有加上
  `--replay-user-messages` 才會，而程式庫不傳這個參數）。

## 一輪對話的結束：`result`

<!-- fixture: edit.stream.ndjson:75 -->
```json
{"type": "result", "subtype": "success", "is_error": false, "duration_ms": 3039, "duration_api_ms": 2777, "num_turns": 4, "total_cost_usd": 0.003943445, "usage": {"input_tokens": 6, "cache_creation_input_tokens": 25845, "cache_read_input_tokens": 50572, "output_tokens": 413}, "fast_mode_state": "off", "result": "Done. Line 3 of `notes.md` now reads `line 3 (edited)`, and `todo.md` was created with two bullet items: \"First item\" and \"Second item\"."}
```

- `subtype` 為 `success` 或某種錯誤；在錄製中唯一的錯誤是 `error_during_execution`（被中斷的一輪對話），
  帶有 `is_error: true` 和 `errors` 清單。
- 讀取器加總的輸入權杖：`input_tokens` + `cache_creation_input_tokens` + `cache_read_input_tokens`。
  輸出權杖：`output_tokens`。
- 失敗的一輪對話的文字：有 `result` 就用它；否則用 `errors` 中的第一段文字。
- `duration_api_ms` 從處理程序啟動時開始計算，而不是從這一輪對話開始。

:::caution
成功的 `result` 可能掩蓋本輪稍早的一次拒絕
（[#108132](https://github.com/anthropics/claude-code/issues/108132)）。需要知道哪些被拒絕的人，應讀取權限
請求與 `tool_result`，而不只是 `result`。
:::

## 一輪對話

一輪對話從一則使用者訊息開始，到它的 `result` 為止：

1. 宿主把訊息寫入 stdin；
2. `claude` 寫出 `system/init` 與 `system/status`（`requesting`）；
3. 針對模型的每則回覆：`stream_event`（`message_start`、各區塊、`message_stop`）以及這些區塊的
   `assistant` 行；
4. 針對每個工具：需要權限時是 `can_use_tool` 的 `control_request`，接著是帶有結果的 `user` 行；
5. `result`。

**在一輪對話進行中送出的訊息不會開啟新的一輪。** 在 `queued` 情境中送出了兩則訊息，第二則是在第一則
執行命令時送出的；`claude` 把第二則併入同一輪，串流中只有一個 `system/init` 和一個 `result`。用已送出的
訊息數去對 `result` 的數量是對不起來的。見[傳輸層](../transporte/#忙碌)中的「忙碌」。

背景工作（`system/task_*`）可能在開啟它們的那一輪的 `result` 之後才結束：在 `subagent` 情境中，
`task_notification` 出現在第 76 行，在第 72 行的 `result` 之後。

## 對話記錄

Claude Code 也會把每個工作階段寫入磁碟上的 `.jsonl` 檔（「對話記錄」）。它不是 stdout：它是另一種格式，
有不同的行，用來讀取已經發生的對話。

- **類型：** `user`、`assistant`、`system` 與 `attachment`。在 21 份錄製（`fixtures/*.transcript.jsonl`）
  中出現了 `user`（76）、`assistant`（84）、帶 `compact_boundary`（2）與 `informational`（1）的 `system`，
  以及一個 `queued_command` 的 `attachment`（1）。
- **名稱不同。** 工具的結構化結果是 `toolUseResult`，而不是 `tool_use_result`；工作階段 id 是
  `sessionId`；壓縮的中繼資料可能是 `compactMetadata`，帶有 `preTokens` 和 `postTokens`。
- **沒有分段。** 沒有 `stream_event`、沒有 `control_request`，也沒有 `result`：只有完整的區塊。
- **使用者訊息在裡面**，是帶有文字的 `user` 行，這在 stdout 中不會出現。一輪對話進行中送達的訊息以
  `attachment`（`queued_command`）的形式保存。
- **讀取器沒有狀態：** 每一行單獨讀取。

<!-- fixture: edit.transcript.jsonl:1 -->
```json
{"type": "user", "uuid": "8b8a5b2f-b887-4d8f-baa5-fd637c77371a", "parentUuid": null, "isSidechain": false, "timestamp": "2026-11-25T00:59:51.422Z", "sessionId": "8f8ab1b3-6d82-4572-9c2c-6f5f166d7bd2", "message": {"role": "user", "content": "Use the Edit tool to change line 3 of notes.md to 'line 3 (edited)'. Then use the Write tool to create todo.md with two bullet items. Be brief."}}
```

每種格式產生的事件見[事件](../eventos/)，表中有來源一欄。
