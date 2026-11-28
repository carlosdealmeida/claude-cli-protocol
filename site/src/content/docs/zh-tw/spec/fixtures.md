---
title: Fixtures
description: 真實 claude -p 工作階段的錄製、每份錄製的預期結果，以及程式庫如何證明自己能使用這個通訊協定。
---

:::note
本頁由 AI 翻譯，尚未經母語人士審閱。術語見 [docs/i18n/glossary.md](https://github.com/carlosdealmeida/claude-cli-protocol/blob/master/docs/i18n/glossary.md)。
:::

fixture 是真實 `claude -p` 工作階段的錄製，保存在
[`fixtures/`](https://github.com/carlosdealmeida/claude-cli-protocol/tree/master/fixtures) 中。**一致性以它們
為準：** 任何語言的程式庫，只要讀取每份錄製並產生旁邊的預期結果，就算能使用這個通訊協定。.NET 程式庫通過
了全部 50 份；Delphi 程式庫也將通過同樣的這些。

## 檔案

每個情境四個檔案：

| 檔案 | 意義 |
|---|---|
| `<情境>.stream.ndjson` | `claude -p` 的 stdout，每行一個 JSON |
| `<情境>.transcript.jsonl` | 同一工作階段的對話記錄，也就是 Claude Code 寫入磁碟的檔案 |
| `<情境>.stream.expected.json` | 從 `.stream.ndjson` 得到的事件 |
| `<情境>.transcript.expected.json` | 從 `.transcript.jsonl` 得到的事件 |

錄製只保存 `claude` 寫出的內容。錄製器送進 stdin 的內容寫在每個情境的腳本中，也就是 `tools/cenarios.json`。

## 情境

| 情境 | 腳本做了什麼 | 錄製呈現了什麼 |
|---|---|---|
| `edit` | 要求一次 `Edit` 與一次 `Write`；兩者都允許 | 兩個權限請求、工具結果 |
| `bash` | 要求兩個 shell 命令；允許 | 建議中帶 `addRules` 的請求，`decision_reason_type: subcommandResults` |
| `subagent` | 要求一個讀取三個檔案的子智慧體 | `system/task_*`、帶 `agent_id` 的請求、在 `result` 之後才結束的工作 |
| `error` | 要求讀取一個不存在的檔案 | 一個錯誤的 `tool_result` |
| `denied` | 要求一次 `Write`；附上訊息拒絕 | 以錯誤 `tool_result` 呈現的訊息 |
| `denied-long` | 同上，訊息有四行 | 含換行的長訊息 |
| `interrupted` | 要求一個長命令，不回覆請求就送出 `interrupt` | `control_cancel_request`、`still_queued`、`error_during_execution` 的 `result` |
| `mode-and-model` | 切換模式與模型，再要求一次 `Write`；一律允許 | `set_permission_mode` 與 `set_model` 的回覆、「一律允許」 |
| `image` | 送出一張 PNG 圖片與一個問題 | 帶圖片的 `tool_result`（2.1.286 的錄製） |
| `commands` | 查詢上下文使用情況，並執行 `/hello Ana` 與 `/compact` | `get_context_usage` 的回覆、`<local-command-stdout>`、壓縮 |
| `effort` | 切換 effort 與模型，並查詢設定 | 沒有內容的 `apply_flag_settings`、帶 `applied` 的 `get_settings` |
| `plan` | 在計畫模式下要求一個計畫；切換到 `acceptEdits` 並允許 `ExitPlanMode` | 離開計畫模式 |
| `plan-auto` | 同上，切換到 `auto` | 離開計畫模式進入 auto 模式 |
| `plan-feedback` | 要求一個計畫；附上訊息拒絕 `ExitPlanMode` | 退回給模型的計畫、一次 `AskUserQuestion` |
| `queued` | 在第一則訊息執行命令時送出第二則訊息 | 兩則訊息、一輪對話：一個 `system/init` 與一個 `result` |
| `queued-compact` | 不等待就送出 `/compact`，接著送出一則訊息 | `compact_boundary`，對話記錄中 `queued_command` 的 `attachment` |
| `hook-blocked` | 送出一個被 `UserPromptSubmit` 掛鉤攔下的提示 | `warning` 層級的 `system/informational` |
| `hook-ask` | `PreToolUse` 掛鉤在 `Write` 之前要求確認 | `decision_reason_type: hook`，帶有掛鉤的文字 |
| `error-long` | 執行一個在 stderr 上印出 40 行並以 3 結束的命令 | 很長的錯誤結果、來自檢查的 `decision_reason` |
| `error-wrapped` | 要求對一段不存在的文字執行 `Edit` | 工具的錯誤 |
| `seguranca` | 要求對 `.claude/settings.local.json` 執行 `Write`；拒絕 | `decision_reason_type: safetyCheck` |
| `bash-ansi` | 執行一條帶終端機顏色、響鈴字元與零寬字元的 `printf` | 帶終端機控制碼與不可見字元的工具結果 |
| `advisor` | 使用 `--advisor opus`，要求呼叫一次 advisor | `server_tool_use` 與 `advisor_tool_result` 區塊，結論是加密的 |
| `workflow` | 要求一個只有一個階段、一個智慧體的 workflow | 帶 `local_workflow` 的 `system/background_tasks_changed`，以及 workflow 的 `system/task_*` 行 |
| `stop-task` | 在背景執行 `sleep 60`，兩秒後送出 `stop_task` | 帶 `status: stopped` 的 `task_notification`、空的工作清單、`stop_task` 的回覆 |

共有 25 個已錄製的情境。腳本中還有第 26 個 `subagent-permission`，從未錄製過。二十份錄製來自 Claude Code
2.1.331；`image` 的錄製來自 2.1.286，因為在 2026-11-25 的重新錄製中，模型沒有開啟圖片就作答了，而先前的
錄製是唯一一份工具結果中帶有圖片的錄製。
`bash-ansi`、`advisor`、`workflow` 與 `stop-task` 的錄製來自 2.1.330：它們是在 Claude Visual Chat 中錄製
並從那裡帶過來的。

## 預期結果

中立的 JSON，不包含任何只屬於某一種語言的東西。完整說明見
[fixture 的 README](https://github.com/carlosdealmeida/claude-cli-protocol/blob/master/fixtures/README.md#o-formato-do-resultado-esperado-versão-1)
（葡萄牙文）；重點如下：

<!-- fixture: edit.stream.expected.json:8 -->
```json
{"line": 2, "event": "session_started", "fields": {"session_id": "8f8ab1b3-6d82-4572-9c2c-6f5f166d7bd2", "model": "claude-haiku-5-5", "working_directory": "C:\\demo\\app", "permission_mode": "default", "claude_version": "2.1.331", "terminal_commands": [], "fast_mode_state": null, "ultrathink_available": null}}
```

- **`format`: 1**，以及 `events` 清單，檔案中每行一個事件。
- **`line`** 是錄製中的行號，從 1 開始，不計空行。一行可以產生零個、一個或多個事件，同一行中事件的順序
  有意義。
- **`event`** 與 **`fields`** 的鍵就是[事件](../eventos/)中的名稱。每個事件的鍵集合是固定的；沒有值的
  欄位為 `null`。
- **列舉**使用 `snake_case` 名稱（`input_json`）。**時間長度**使用整數毫秒。**日期**使用 ISO 8601、UTC，
  恰好三位毫秒並以 `Z` 結尾，截斷而不四捨五入。
- **圖片：** `data` 變成 `{"length": <base64 的字元數>, "sha256": "<base64 文字 UTF-8 位元組的小寫十六進位>"}`。
- **比較是結構化的。** 鍵的順序不重要；清單項目的順序重要。兩個數字相差不超過 `1e-9` 即視為相等。

## 程式庫如何證明一致性

1. 對每個 `<情境>.stream.ndjson`：使用一個新的、**在行與行之間保有狀態**的讀取器（每個
   `parent_tool_use_id` 最後一個 `message_start` 的 id）逐行讀取檔案。
2. 對每個 `<情境>.transcript.jsonl`：使用一個**沒有狀態**的讀取器單獨讀取每一行。
3. 程式庫依上述格式寫出事件，並與 `.expected.json` 做結構化比較。
4. 任何差異都是失敗，並附上到差異值的路徑（例如 `events[12].fields.block.input.file_path`）。

行以 `\n` 分隔；結尾的 `\n` 會被忽略，空行不計。

### 通過代表什麼

50 份預期結果是由 .NET 程式庫寫出的。通過它們，證明一個程式庫讀取錄製的方式與 .NET 程式庫相同，而且行為的
變化會以差異呈現。光憑這一點，並不能證明每個欄位都是對的。有三個情境（`edit`、`denied` 與 `mode-and-model`）
以一個讀取原始錄製的腳本從外部核對過。錄製涵蓋了讀取器 36 種事件中的 27 種；另外九種見
[事件](../eventos/#沒有任何錄製涵蓋的事件)。

## 去識別化

錄製是真實的工作階段，任何錄製都不能帶有錄製者的個人資料。錄製器（`tools/gravar.py`）：

- 把資料夾、家目錄與使用者名稱換成 `demo` 的（`C:\demo\app`、`C:\Users\demo`），即使路徑被拆在一個區塊的
  多個 delta 中也一樣；
- 把 Claude Code 在 `Temp\claude` 下建立的專案資料夾換成 `demo` 資料夾的名稱（`C--demo-app`）：從
  2.1.330 起，這個資料夾的名稱是由錄製者的資料夾得出的 16 位十六進位數字；
- 把所有 `encrypted_content`（例如 advisor 結論中的）換成 `redacted`；
- 在 `system/init` 行中只保留已知的欄位，並移除 `mcp__` 工具；
- 在 `initialize`、`get_settings` 與 `get_context_usage` 的回覆中只保留已知的欄位，並移除帳戶；
- 移除 `rate_limit_event` 行（它們屬於錄製者的帳戶）以及 `result` 中的 `credential_expires_at`。

隱私測試（`FixturePrivacyTests`）會在每個檔案中尋找其他使用者的資料夾、以雜湊命名的專案資料夾、看起來像憑證的內容與電子郵件地址，並
說明在哪一行、哪個欄位找到，但不顯示值。兩者都不理解對話的文字：錄製者在送出前要自己讀過錄製。

## 每週重新錄製

每週一，`fixtures.yml` 工作流程會用最新的 `claude` 重新錄製已有錄製的情境，分成兩個作業：

1. 執行真實 `claude` 的作業，沒有儲存庫的寫入權限：錄製、重新寫出預期結果，並對新檔案執行一致性測試與隱私
   測試；
2. 開啟提取要求的作業，把錄製與預期結果一起提交，不執行 `claude`。

工作階段與訊息的 id 每次錄製都會改變，所以每週都有一個提取要求。由於預期結果是程式庫自己寫的，能擋下一份
糟糕錄製的，是閱讀提取要求的人：一個事件不見了，一個欄位的結構變了。**這樣，CLI 的變化會先在這裡出現，而不
是在使用中才被發現。** 第一次完整跑完的重新錄製是 2026-11-25 那一次。

## 手動錄製

```bash
python -I tools/gravar.py edit        # 一個情境
python -I tools/gravar.py --todos     # 全部
python -I tools/test_gravar.py        # 去識別化程式的測試（不會啟動 claude）
```

錄製器會在一個暫存資料夾中、以本機的帳戶啟動 PATH 中的 `claude`（或 `--claude <路徑>` 指定的那個）。錄製之後，
由 .NET 程式庫重新寫出預期結果：

```bash
CCP_UPDATE_EXPECTED=1 dotnet test libs/dotnet -c Release --framework net6.0 --filter "FullyQualifiedName~ConformanceTests"
```
