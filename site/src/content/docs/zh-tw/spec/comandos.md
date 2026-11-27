---
title: 命令
description: 宿主寫入 claude -p 的 stdin 的內容——帶有文字、圖片與 uuid 的使用者訊息——以及這一行如何編碼。
---

:::note
本頁由 AI 翻譯，尚未經母語人士審閱。術語見 [docs/i18n/glossary.md](https://github.com/carlosdealmeida/claude-cli-protocol/blob/master/docs/i18n/glossary.md)。
:::

宿主透過在 stdin 上每個命令寫一行 JSON 來與 `claude` 溝通。行有三種形式：

| `type` | 意義 | 位置 |
|---|---|---|
| `user` | 一則使用者訊息 | 本頁 |
| `control_request` | 宿主送給 `claude` 的控制請求：`initialize`、interrupt、切換模式或模型…… | [控制](../controle/#從宿主到-claude) |
| `control_response` | 宿主對 `claude` 的請求（例如權限請求）的回覆 | [控制](../controle/#從-claude-到宿主) |

`fixtures/` 中的錄製只保存 stdout。本頁的行來自 .NET 程式庫的 `StdinWriter` 與錄製器
（`tools/gravar.py`），錄製中的訊息就是錄製器送出的。

## 編碼

- **每個命令一行**，以 `\n` 結尾，每行寫完後 flush。
- **只用 ASCII。** 超出 ASCII 的字元會跳脫為 `\uXXXX`（`ã` 變成 `\u00E3`）。不論系統給管線什麼編碼，這一行
  抵達 `node` 時都一樣；在 Windows 上，子處理程序的管線不一定是 UTF-8。這是程式庫的規則，不是 `claude`
  的規則：它讀的是 JSON，UTF-8 的 `ã` 也是有效的 JSON。程式庫選擇不依賴管線的編碼。
- **不要用關閉 stdin 的方式結束一輪對話。** 使用 `--input-format stream-json` 時，一輪對話之後 stdin 結束
  會立刻結束處理程序，並遺失尚未處理的內容
  （[#111427](https://github.com/anthropics/claude-code/issues/111427)）。關閉 stdin 就是結束工作階段，只能
  刻意為之。

## 使用者訊息

<!-- stdin -->
```json
{"type":"user","message":{"role":"user","content":"Say hi in one line."},"parent_tool_use_id":null,"session_id":"default"}
```

- **`message.content`** 在訊息沒有圖片時就是文字。
- **`parent_tool_use_id`** 為 `null`：訊息是給主對話的。
- **`session_id`** 為 `"default"`。`claude` 不會用它來選擇工作階段：工作階段就是這個處理程序的工作階段，
  新的或 `--resume` 指定的。真正的 id 在 `system/init` 行中給出。

同樣的文字會出現在對話記錄中，作為第一個 `user` 行（見[通訊協定](../protocolo/#對話記錄)）。它不會回到
stdout。

### 帶圖片

有圖片時，`content` 變成清單：先依序放圖片區塊，再放文字區塊。空的文字不產生區塊。

<!-- stdin -->
```json
{"type":"user","message":{"role":"user","content":[{"type":"image","source":{"type":"base64","media_type":"image/png","data":"iVBORw0KGgo…"}},{"type":"text","text":"What two colors do you see? One line."}]},"parent_tool_use_id":null,"session_id":"default"}
```

`data` 是檔案的 base64，不含 `data:` 前綴。fixture 中的 `image` 情境就是這樣送出一張 96 × 48 的 PNG 圖片。

### 帶 `uuid`

<!-- stdin -->
```json
{"type":"user","message":{"role":"user","content":"Say hi in one line."},"parent_tool_use_id":null,"session_id":"default","uuid":"5d0c9a3e-1b7f-4a52-9e64-0c2f8b1d7a41"}
```

帶上 `uuid` 後，`claude` 會用 `command_lifecycle` 行說明這則訊息的去向（自 2.1.310 起）：`command_uuid`
是送出的 uuid，`state` 為 `queued`、`started`、`completed`、`cancelled`、`discarded` 或 `refused`。
`ran_ahead: true` 表示 `claude` 在工作中立刻執行了這個命令。沒有任何錄製包含這些行；對應的事件是
[`command_lifecycle`](../eventos/#沒有任何錄製涵蓋的事件)。

:::caution[絕對不要用另一則訊息的 `uuid` 重送]
2.1.327 修正了一個問題：處理程序當掉後以同一個 uuid 重送訊息，會遺失已保存的訊息。每則訊息有自己的
uuid；重送就是送出一則新訊息，帶新的 uuid。
:::

### 帶 `inline_pastes`

<!-- stdin -->
```json
{"type":"user","message":{"role":"user","content":"…"},"parent_tool_use_id":null,"session_id":"default","inline_pastes":["…"]}
```

`inline_pastes` 列出仍在 `content` 中的長段貼上文字。在對話記錄中，每段長的貼上內容都包在一個外殼裡：一行
`<pasted_content id="abcd">`（四位小寫十六進位數字）、貼上的文字、一行 `</pasted_content id="abcd">`，
程式庫的讀取器會移除外殼，照原樣顯示訊息。沒有任何錄製包含貼上內容。

## 附加的檔案

通訊協定沒有檔案區塊。程式庫採用的方式是在文字結尾每個檔案加一行，寫上該檔案在 `claude` 所在機器上的路徑：

```
摘要這個檔案。
[attached file: C:\demo\app\notes.md]
```

`claude` 用自己的工具從那裡讀取檔案。回讀對話的一方會把這些行從訊息結尾移除（只移除結尾的，而且只移除完全
符合標記格式的），並另外顯示檔案。這是程式庫與 Claude Visual Chat 的慣例，不是 `claude` 的慣例。

## 斜線命令

斜線命令（`/compact`、`/context`、外掛提供的命令）以一般訊息的文字送出：

<!-- stdin -->
```json
{"type":"user","message":{"role":"user","content":"/compact"},"parent_tool_use_id":null,"session_id":"default"}
```

工作階段提供的命令會在 `initialize` 的回覆（`commands`）中給出，有變化時會有一行
`system/commands_changed`。本機命令的輸出透過帶有 `<local-command-stdout>` 的 `user` 行回來；在
`commands` 情境中，`/compact` 回傳 `<local-command-stdout>Compacted </local-command-stdout>`。

:::note
在 2.1.314 之前，文字中帶有 `claude` 本身標記（例如 `<bash-stdout>`）的訊息不會向本機的 stream-json
宿主確認。程式庫帶 `uuid` 送出這樣的訊息時會看到什麼，尚未核對（[ROADMAP](https://github.com/carlosdealmeida/claude-cli-protocol/blob/master/docs/ROADMAP.md) 第 6 項）。
:::
