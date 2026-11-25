---
title: 控制
description: 兩個方向的控制請求——從宿主到 claude，從 claude 到宿主——、權限請求、回覆及其規則。
---

:::note
本頁由 AI 翻譯，尚未經母語人士審閱。術語見 [docs/i18n/glossary.md](https://github.com/carlosdealmeida/claude-cli-protocol/blob/master/docs/i18n/glossary.md)。
:::

控制請求是帶有 `request_id` 的 `control_request` 行，回覆是重複這個 id 的 `control_response` 行。請求有
兩個方向：

- **從宿主到 `claude`**：`initialize`、interrupt、切換模式、模型與 effort、各種查詢；
- **從 `claude` 到宿主**：權限請求（`can_use_tool`），以及掛鉤、MCP 與 elicitation 的請求，本通訊協定
  不處理後者。

## 形式

請求：

<!-- stdin -->
```json
{"type":"control_request","request_id":"mode-0","request":{"subtype":"set_permission_mode","mode":"acceptEdits"}}
```

成功的回覆，內容在 `response.response` 中（有內容時）：

<!-- fixture: mode-and-model.stream.ndjson:2 -->
```json
{"type": "control_response", "response": {"subtype": "success", "request_id": "mode-0", "response": {"mode": "acceptEdits"}}}
```

錯誤的回覆，文字在 `response.error` 中：

<!-- stdin -->
```json
{"type":"control_response","response":{"subtype":"error","request_id":"…","error":"not supported"}}
```

- **`request_id` 由提出請求的一方決定。** 宿主為自己的請求選擇 id；`claude` 為它的請求選擇 id。程式庫
  為每個請求產生新的 id（32 位、不含連字號的 GUID）：呼叫端不選。錄製器使用容易閱讀的 id（`init-1`、
  `mode-0`），所以錄製中出現的是這些。
- **回覆只給出 id，不說明它回覆的是哪個請求。** 提出請求的一方以 id 保存請求，才知道回覆確認了什麼。
  在程式庫中，由 `ClaudeSession` 負責，並把這一對以 `control_replied` 交出。
- **順序沒有保證。** 回覆可能出現在一輪對話的各行之間。

## 從宿主到 claude

| `subtype` | 請求內容 | 請求內容主體 | 回覆內容主體 | 是否錄製 |
|---|---|---|---|---|
| `initialize` | 交握；官方 SDK 最先送出它 | — | `commands`、`models`、`account`、`auth_conflict` | 全部 |
| `interrupt` | 停止這一輪對話 | — | `still_queued`：仍在排隊的訊息 | `interrupted` |
| `set_permission_mode` | 切換模式 | `mode` | `mode` | `mode-and-model`、`plan*` |
| `set_model` | 切換模型 | `model` | `model`，已解析 | `mode-and-model`、`effort` |
| `apply_flag_settings` | 切換 effort | `settings.effortLevel`（文字或 `null`） | 無 | `effort` |
| `get_settings` | 目前生效的設定 | — | `applied`（`model`、`effort`） | `effort` |
| `get_context_usage` | 上下文視窗的使用情況 | — | `categories`、`totalTokens`、`maxTokens`、`percentage` | `commands` |
| `get_task_output` | 某個背景工作目前已輸出的內容 | `task_id` | — | 否 |
| `remote_control` | 開啟或關閉 Remote Control | `enabled` | 開啟時為 `session_url` | 否 |
| `get_chrome_dialog` | 瀏覽器擴充功能的狀態 | — | — | 否 |

### `initialize`

<!-- stdin -->
```json
{"type":"control_request","request_id":"init-1","request":{"subtype":"initialize"}}
```

回覆列出工作階段的斜線命令、模型與帳戶：

<!-- fixture: edit.stream.ndjson:1 -->
```json
{"type": "control_response", "response": {"subtype": "success", "request_id": "init-1", "response": {"commands": [{"name": "compact", "description": "Free up context by summarizing the conversation so far", "argumentHint": "<optional custom summarization instructions>", "builtin": true}, "…"]}}}
```

- `commands[]`：`name`（不含斜線）、`description`、`argumentHint`。
- `models[]`：`value`、`resolvedModel`、`displayName`、`description` 與 `supportedEffortLevels`。
- `account`：登入方式（`apiProvider`、`tokenSource`、`apiKeySource`）、`email`、`organization` 與
  `subscriptionType`。錄製中沒有它：錄製器會移除，因為那是錄製者的帳戶。
- `auth_conflict`：同時有權杖與 API key 時，兩個來源的名稱（`token_source` 與 `api_key_source`），絕不是
  憑證本身。

所有錄製都以第 1 行的 `initialize` 回覆開始，因為錄製器在任何訊息之前先送出它。`system/init` 行要到第一則
訊息時才會抵達。

### `interrupt`

<!-- fixture: interrupted.stream.ndjson:22 -->
```json
{"type": "control_response", "response": {"subtype": "success", "request_id": "interrupt-1", "response": {"still_queued": []}}}
```

被中斷的一輪對話以 `error_during_execution` 的 `result` 結束。在此之前，尚未回覆的權限請求會被取消
（見 [`control_cancel_request`](#control_cancel_request)）。

### `set_permission_mode` 與 `set_model`

模式或模型從成功的回覆之後開始生效。`set_model` 的回覆帶有解析後的模型：在 `effort` 情境中，請求 `haiku`
回傳的是 `claude-haiku-5-5`。

<!-- fixture: effort.stream.ndjson:14 -->
```json
{"type": "control_response", "response": {"subtype": "success", "request_id": "model-3", "response": {"model": "claude-haiku-5-5"}}}
```

程式庫接受請求的模式為 `plan`、`default`、`acceptEdits` 與 `auto`。不在清單中的模式不會被寫出（見
[傳輸層](../transporte/#檢查)）。

### `apply_flag_settings` 與 `get_settings`

<!-- stdin -->
```json
{"type":"control_request","request_id":"effort-1","request":{"subtype":"apply_flag_settings","settings":{"effortLevel":"high"}}}
```

回覆不確認任何東西：它是一個沒有內容的成功回覆。目前生效的 effort 在 `get_settings` 的回覆中給出：

<!-- fixture: effort.stream.ndjson:2 -->
```json
{"type": "control_response", "response": {"subtype": "success", "request_id": "effort-1"}}
```

<!-- fixture: effort.stream.ndjson:3 -->
```json
{"type": "control_response", "response": {"subtype": "success", "request_id": "settings-1", "response": {"applied": {"model": "claude-haiku-5-5", "effort": "high"}}}}
```

`effortLevel: null` 恢復為模型的預設 effort。自 2.1.325 起，無效的值會以錯誤回覆拒絕。

### `get_context_usage`

<!-- fixture: commands.stream.ndjson:12 -->
```json
{"type": "control_response", "response": {"subtype": "success", "request_id": "context-2", "response": {"categories": [{"name": "System prompt", "tokens": 3129, "kind": "used"}, "…"], "totalTokens": 25341, "maxTokens": 1000000, "percentage": 3}}}
```

`kind` 為 `used`、`deferred`、`buffer` 或 `free`。有的話，`model`、`memoryFiles` 與 `agents` 會補充這個回覆。

### 如何讀取回覆內容

程式庫不知道一則回覆回答的是哪個請求：它依內容的結構來讀取。

| 內容中有 | 讀取為 |
|---|---|
| `commands` | 斜線命令；如果還有 `models` 或 `account`，再讀取 `auth_conflict` |
| `models` | 模型 |
| `applied` | 目前生效的設定 |
| `categories` 與 `totalTokens` | 上下文的使用情況 |
| `account` | 帳戶 |
| `rate_limits` | 用量限制（`get_usage` 的回覆；程式庫不送出這個請求，但會讀取） |
| `session_url` | Remote Control 的連結 |

## 從 claude 到宿主

### `can_use_tool`，權限請求

使用 `--permission-prompt-tool stdio` 時，每個需要權限的工具都會在 stdout 上變成一個請求，`claude` 在
stdin 上等待回覆：

<!-- fixture: edit.stream.ndjson:40 -->
```json
{"type": "control_request", "request_id": "0f8155c2-5135-4e0b-9da0-4cbc9d41ee1e", "request": {"subtype": "can_use_tool", "tool_name": "Edit", "display_name": "Edit", "input": {"file_path": "C:\\demo\\app\\notes.md", "old_string": "line three\n", "new_string": "line 3 (edited)\n", "replace_all": false}, "description": "notes.md", "permission_suggestions": [{"type": "setMode", "mode": "acceptEdits", "destination": "session"}], "tool_use_id": "toolu_01FPyYmYbFzFZR24ZJtHzcWB"}}
```

| 欄位 | 意義 |
|---|---|
| `tool_name` | 工具。少了它，程式庫不產生事件，傳輸層以錯誤回覆 |
| `display_name`、`description` | 如何顯示這個請求 |
| `input` | 工具將要做的事，與模型要求的一致 |
| `permission_suggestions` | `claude` 建議保存的規則，以免再次詢問 |
| `tool_use_id` | 這個請求對應的工具呼叫 |
| `agent_id` | 提出請求的子智慧體（它的 `task_id`）；主對話中沒有 |
| `decision_reason`、`decision_reason_type`、`decision_reason_code` | `claude` 為什麼詢問：掛鉤的文字、安全檢查的說明……類型為 `hook`、`safetyCheck`、`subcommandResults`、`other`…… |
| `default_to_no` | `claude` 要求預設選取「否」 |
| `suppress_always_allow_rule` | `claude` 不提供可保存的規則：沒有「一律允許」 |
| `mcp_server.source` | 工具的 MCP 伺服器來自哪裡；claude.ai 連接器為 `claudeai` |

錄製中出現了 `Edit`、`Write`、`Bash`、`PowerShell`、`ExitPlanMode` 與 `AskUserQuestion` 的請求。子智慧體
的請求帶有 `agent_id`（`subagent.stream.ndjson` 第 50 行）；掛鉤的請求帶有 `decision_reason_type: "hook"`
（`hook-ask` 第 20 行）；受保護資料夾的請求帶有 `decision_reason_type: "safetyCheck"`（`seguranca` 第 20 行）。

### 權限回覆

三種決定，以帶有該請求 `request_id` 的 `success` 類型 `control_response` 回覆：

**允許**：`input` 原樣放回 `updatedInput`。

<!-- stdin -->
```json
{"type":"control_response","response":{"subtype":"success","request_id":"0f8155c2-5135-4e0b-9da0-4cbc9d41ee1e","response":{"behavior":"allow","updatedInput":{"file_path":"C:\\demo\\app\\notes.md","old_string":"line three\n","new_string":"line 3 (edited)\n","replace_all":false}}}}
```

**一律允許**：同上，再把請求中的 `permission_suggestions`（若為清單）放進 `updatedPermissions`。

<!-- stdin -->
```json
{"type":"control_response","response":{"subtype":"success","request_id":"…","response":{"behavior":"allow","updatedInput":{"…":"…"},"updatedPermissions":[{"type":"setMode","mode":"acceptEdits","destination":"session"}]}}}
```

**拒絕**：附上給模型的訊息。沒有訊息時，程式庫會送出 `"The user denied this tool use."`。

<!-- stdin -->
```json
{"type":"control_response","response":{"subtype":"success","request_id":"…","response":{"behavior":"deny","message":"Do not create files, just explain."}}}
```

這則訊息會以 `is_error: true` 的 `tool_result` 文字抵達模型。在 `denied` 情境中，模型讀到訊息後在 `result`
中說明它沒有建立檔案。

:::note[離開計畫模式]
計畫的結束是一個 `ExitPlanMode` 的權限請求。要離開計畫模式並依計畫繼續，錄製器會先送出
`set_permission_mode` 切換到下一個模式（`plan` 情境為 `acceptEdits`，`plan-auto` 為 `auto`），再允許。
附上訊息拒絕，則會把計畫連同這則訊息退回給模型（`plan-feedback` 情境）。
:::

### 權限回覆的規則

> 對權限請求的回覆，一律帶有處理程序一側自己從 `claude` 收到的 `input` 與 `permission_suggestions`。
> 呼叫端只選擇 `request_id`、決定與訊息。

這是程式庫的規則，不是 `claude` 的規則：它會接受收到的任何 `updatedInput`，這個欄位本來就是為此而設。
但持有某個 `claude` 的 stdin 的一方，不應執行來自別處的 `input`。如果請求經過介面、網路或另一個處理程序，
回覆的一方可能把 `"command": "ls"` 換成別的命令，而 `claude` 會以給第一個命令的許可去執行被換掉的命令。

在程式庫中：

- **回覆命令（`AnswerPermission`）沒有 `input` 欄位**，也沒有建議欄位：只有 id、決定與訊息；
- **寫入 stdin 的一方以它自己從 stdout 讀到的請求來組成回覆**，這些請求保存在請求簿
  （`PermissionLedger`）中；
- **每個請求只回覆一次。** 回覆會把請求從簿中取出。`claude` 取消請求、該呼叫的 `tool_result` 抵達，或這
  一輪對話結束時，請求也會被移出。不在待處理狀態的 id（已回覆、已取消、已解決或未知）不會寫出任何內容。

自 2.1.321 起，`AskUserQuestion` 會拒絕修改了 `input` 的宿主的回覆（欄位 `afkTimeoutMs`）。遵守這條規則，
就不會發生這種情況。

:::caution[尚未核對]
使用 `--permission-prompt-tool` 時，較大的 "allow" 結果會被 MCP 的大型輸出處理寫入檔案
（[#109249](https://github.com/anthropics/claude-code/issues/109249)）。回覆會把整個 `input` 送回，所以寫入
大型檔案的 `Write` 會經過這裡。還需要用真實的 `claude` 驗證。
:::

### `control_cancel_request`

`claude` 放棄一個尚未得到回覆的權限請求：

<!-- fixture: interrupted.stream.ndjson:21 -->
```json
{"type": "control_cancel_request", "request_id": "dbfbeade-6899-46b2-ba43-120f8dea7acd"}
```

在 `interrupted` 情境中，第 20 行的 `PowerShell` 請求在第 21 行被 interrupt 取消。取消之後，請求從簿中移出，
對它的回覆不會寫出任何內容。

### 無人處理的請求

`claude` 也可能送出其他類型的控制請求：SDK 掛鉤的回呼、給 SDK MCP 伺服器的訊息、elicitation。沒有回覆的話，
它會一直等待。程式庫的傳輸層對其中任何一種都以錯誤回覆：

<!-- stdin -->
```json
{"type":"control_response","response":{"subtype":"error","request_id":"…","error":"not supported"}}
```

而讀取器無法讀取的 `can_use_tool`（例如沒有 `tool_name`），因為永遠到不了回覆的一方，會收到錯誤
`"malformed permission request"`。
