---
title: 傳輸層
description: 負責傳遞行與命令的部分——介面、claude 處理程序、參數與其檢查、啟動、結束與已知限制。
---

:::note
本頁由 AI 翻譯，尚未經母語人士審閱。術語見 [docs/i18n/glossary.md](https://github.com/carlosdealmeida/claude-cli-protocol/blob/master/docs/i18n/glossary.md)。
:::

傳輸層負責把 `claude` 的行往一個方向傳遞，把命令往另一個方向傳遞。它可以是處理程序本身、通往遠端 `claude`
的網路，或一份錄製。本頁說明傳輸層做什麼，並詳細說明在本機啟動 `claude` 的那一個（.NET 程式庫的
`ProcessTransport`）做什麼。這裡以規則寫出的是程式庫的規則；屬於 `claude` 的行為會另外說明。

## 介面

介面以命令為單位，而不是原始的 stdin：

```csharp
public interface IClaudeTransport : IAsyncDisposable
{
    /// The next stream-json line, or null once the transport has closed.
    Task<string?> ReceiveLineAsync(CancellationToken ct);

    /// A command; each transport delivers it in its own way.
    Task SendAsync(ClaudeCommand command, CancellationToken ct);

    /// Raised when the transport drops, comes back or closes.
    event EventHandler<TransportState>? StateChanged;
}
```

- **行往上，命令往下。** 使用傳輸層的一方逐行接收 stdout 的行，並送出命令：一則訊息、一個權限回覆、一個
  控制請求。把命令的行寫入 stdin 的是處理程序一側。
- **為什麼用命令。** 權限回覆由處理程序一側以它讀到的請求組成（見[規則](../controle/#權限回覆的規則)）。
  如果是原始 stdin，呼叫端可以寫入任何 `input`。
- **狀態：** `Open`（行進來，命令出去）、`Dropped`（斷線了，可能恢復：會重新連線的網路傳輸層）與
  `Closed`（已結束：不會再有行，每次讀取都傳回 null）。
- **程式庫中的傳輸層：** `ProcessTransport` 啟動 `claude`；`ReplayTransport` 播放一份錄製並忽略命令。網路
  傳輸層屬於使用它的應用程式，而不屬於程式庫。

## 處理程序

### 參數

依這個順序，不加其他參數：

```
-p --input-format stream-json --output-format stream-json --verbose
--include-partial-messages --permission-prompt-tool stdio --permission-mode <modo>
[--resume <id>] [--model <modelo>] [--effort <nível>]
```

後三個只在有要求時才加。**絕不使用 `--setting-sources`：** 每台機器的封鎖規則在任何模式下都有效。

### 檢查

在 Windows 上，透過 npm 安裝的 `claude` 是一個 `claude.cmd`，會經過 `cmd.exe`。在那裡，值中一個多出的字元
就會變成另一個命令。所以每個進入命令列的值都會檢查，被拒絕的值不會啟動任何東西：

| 值 | 接受 |
|---|---|
| 模式 | `plan`、`default`、`acceptEdits` 或 `auto` |
| 模型 | `^[A-Za-z0-9][A-Za-z0-9._:@/\[\]-]{0,99}$`：別名（`sonnet`、`opus[1m]`）、id、Bedrock 設定檔（`arn:…`）與 Vertex id（`…@20250514`） |
| 工作階段 id | `^[A-Za-z0-9][A-Za-z0-9_-]{0,199}$` |
| effort | `low`、`medium`、`high`、`xhigh` 或 `max` |
| 資料夾 | 不可為空 |
| `claude` 的路徑 | 有給時必須是絕對路徑 |

執行中要求切換模式時也一樣：模式不在清單中的 `set_permission_mode` 不會被寫出。檢查會擋下的值舉例：模型
`sonnet & calc`、工作階段 id `a" --dangerously-skip-permissions`、模式 `bypassPermissions`。

### 尋找 `claude`

沒有給路徑時，傳輸層會在 PATH 中尋找 `claude`，**只在其中的絕對路徑資料夾裡找，絕不在目前資料夾裡找**：
目前資料夾就是對話的資料夾，放在那裡的 `claude.cmd` 不能被當成要執行的那一個。

### 環境

- 本機的環境，加上宿主為這個 `claude` 傳入的變數（值為 null 的變數會被刪除）。其中包括選擇帳戶的
  `CLAUDE_CONFIG_DIR`。傳輸層不依名稱辨識任何變數。
- 再加上 **`NoDefaultCurrentDirectoryInExePath=1`**，讓 `cmd.exe` 不在對話的資料夾裡尋找 `node`。
- 帳戶是本機的：訂閱登入或 `ANTHROPIC_API_KEY`。把聊天嵌入第三方產品的人應該使用 API key。

### stdin、stdout 與 stderr

- **stdin：** 每個命令一行，ASCII 位元組，帶 `\n` 並 flush（見[命令](../comandos/#編碼)）。開始寫的一行會
  寫完：半行會破壞串流。傳輸層從不自行關閉 stdin。
- **stdout：** UTF-8，逐行讀取。
- **stderr：** 讀取後丟棄。必須讀取，否則管線會塞滿，`claude` 會卡住；但不保存，因為其中可能有對話的文字。
  程式庫只計算行數。
- **兩路讀取同時進行，各自在自己的執行緒上。** 先把 stdout 讀完再讀 stderr 會讓兩邊都卡住。在程式庫中，
  每路讀取都有自己的執行緒：如果使用執行緒集區的執行緒，每個執行中的 `claude` 會在整個生命週期占用其中
  三個，在 4 核心的機器上兩個傳輸層就足以讓程式的其他部分等待。

## 啟動

1. 檢查各個值並組成參數。
2. 在對話的資料夾中啟動處理程序。
3. **開始讀取 stdout 與 stderr。**
4. **寫入 `initialize`**，與官方 SDK 相同（可以關閉）。

第 3 步與第 4 步的順序很重要。測試中發現：在 `netstandard2.0` 組建、.NET Framework 下，如果 `claude` 在讀取
stdin 之前寫了很多內容，`initialize` 的寫入不會完成；要有人讀取 stdout 後才會完成。確切原因尚未查明。

- **延遲。** 自 2.1.323 起有回報指出到第一個位元組需要 5 到 7 秒
  （[#109534](https://github.com/anthropics/claude-code/issues/109534)）；2.1.325 開始在第一個請求之前等待
  帳戶資料，2.1.327 最多等待 10 秒取得模型設定。啟動時限太短會失敗。
- **啟動失敗**會關閉傳輸層：讀取傳回 null，結束代碼可供取得。自 2.1.314 起，無法啟動的 `claude -p` 會在
  stderr 上印出錯誤並以 1 結束（之前以 0 結束且沒有輸出）。由於 stderr 會被丟棄，結束代碼是剩下的唯一訊號。
- **在啟動過程中處置**不會留下處理程序：傳輸層在啟動 `claude` 之後再檢查一次，若已被處置，就結束整個處理
  程序樹。

## 忙碌

`ClaudeSession` 會說明工作階段是否忙碌。規則跟著串流走，而不是訊息計數：

> 從送出一則訊息或收到一個 `system/init` 起，直到下一個 `result`，工作階段處於忙碌狀態。

以輪計數（每則訊息開啟一輪，每個 `result` 結束一輪）是對不起來的：在 `queued` 情境中，兩則訊息只產生了
一輪（見[通訊協定](../protocolo/#一輪對話)）。這條規則的代價是：排隊等候的訊息會讓工作階段在上一輪的
`result` 和它自己的 `system/init` 之間短暫空閒。

工作階段也會保存已確認的 `session_id`、模式與模型（來自 `system/init`、`system/status` 以及請求的成功回覆），
以及待處理的權限請求。傳輸層關閉之後送出的命令不會送出，不會讓工作階段變成忙碌，並傳回 `false`。

## 結束

- **結束來自處理程序，而不是 stdout 的結束。** 繼承了 stdout 的 `claude` 子處理程序可能讓它一直保持開啟。
  處理程序結束後，傳輸層會在有限的時間內（程式庫中為 10 秒）等待最後幾行（也就是 `result`），然後關閉。
- **結束代碼**在結束後可供取得。
- **結束就是立即終止整個處理程序樹。** 不先嘗試溫和地結束：`claude` 自行結束後，它的子處理程序就無法再透過
  它的 pid 找到，等待會留下處理程序。代價是：如果宿主在一輪對話進行中處置它，`claude` 不會完成寫入磁碟的
  動作。
- **結束之後，送出什麼都不做：** 不寫入，也不擲回管線例外。

## 已知限制

- 在 Windows 以外的 `netstandard2.0` 上，結束時只終止 `claude`，不終止它的子處理程序。
- 在自行結束的 `claude` 之後仍存活的子處理程序不會被結束。
- `ClaudeLocator` 不檢查執行權限位元。
- 孤兒子處理程序占著 stdout 時，讀取執行緒會持續存活，但結束之後不會再交出任何行。
- 對真實的 `claude`（2.1.331，Windows），傳輸層只執行過一次，送出一則簡短訊息，該測試只在明確要求時執行
  （`CCP_LIVE_CLAUDE=1`）。其餘測試使用模擬的 `claude`。

## 傳輸層絕不做的事

- 把對話、stderr 或憑證寫入磁碟。
- 傳入清單之外的參數，或未經檢查的值。
- 用不是自己從 `claude` 讀到的 `input` 寫出權限回覆。
- 在有待處理訊息時關閉 stdin。
