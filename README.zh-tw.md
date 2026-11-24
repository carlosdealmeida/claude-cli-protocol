# claude-cli-protocol

[Português](README.md) · [English](README.en.md) · [Español](README.es.md) · [简体中文](README.zh-cn.md) · **繁體中文**

> 本譯文由 AI 產生，尚未經母語人士審校。歡迎指正，詞彙表見 [docs/i18n/glossary.md](docs/i18n/glossary.md)。

[![CI](https://github.com/carlosdealmeida/claude-cli-protocol/actions/workflows/ci.yml/badge.svg)](https://github.com/carlosdealmeida/claude-cli-protocol/actions/workflows/ci.yml)
[![License: MIT](https://img.shields.io/badge/License-MIT-yellow.svg)](LICENSE)

**`claude -p` 的 stream-json 通訊協定：有規格、有錄製、有實作。**
[Claude Code](https://github.com/anthropics/claude-code) 以 `-p` 執行時，每則訊息是一行 JSON。
本儲存庫把目前分散的三樣東西放在一起：這個通訊協定的書面規格、用來驗證規格的真實工作階段錄製，
以及實作該通訊協定的程式庫（先是 .NET，接著是 Delphi）。

> 社群專案，與 Anthropic 無隸屬關係，亦未獲其認可。

## 狀態

**第 2 階段，截至 2026-11-24。** .NET 程式庫已能讀寫該通訊協定，並能啟動本機的 `claude -p`：事件、兩個剖析器、命令、工作階段、錄製重播和 `ProcessTransport`。它在兩個目標框架上通過了 `fixtures/` 中的 42 份錄製。傳輸層使用模擬的 `claude` 進行測試；對真實的 `claude`（2.1.331，Windows）它執行過一次，傳送了一則簡短訊息，該測試僅在明確要求時執行。尚未發佈任何套件，網站也尚未上線。

## 這裡有什麼

| 資料夾 | 內容 |
|---|---|
| `site/` | 首頁和規格，使用 Astro + Starlight，五種語言 |
| `docs/` | 設計文件（spec）、計畫、ROADMAP 和翻譯詞彙表 |
| `fixtures/` | `claude -p` 的錄製，以及每份錄製的預期結果 |
| `libs/dotnet/` | `ClaudeCliProtocol`，目標為 `netstandard2.0` 和 `net6.0` |
| `libs/delphi/` | Delphi 程式庫，最低支援 10.1 Berlin |
| `examples/` | 主控台範例 |
| `tools/` | 儲存庫結構測試和套件檢查 |

## 分層

```
claude-cli-protocol（程式庫）  ← 只負責通訊協定；不知道任何介面
   ↑
ClaudeVisualChat.Avalonia      ← 只負責繪製；不啟動處理程序，也不存取網路
   ↑
你的應用程式                   ← 負責整合
```

每一層只認識它下面的一層，並透過帶版本號的套件相依。本儲存庫不包含任何介面，任何語言都不例外。

## 如何呼叫 claude -p

```
claude -p --input-format stream-json --output-format stream-json --verbose
       --include-partial-messages --permission-prompt-tool stdio --permission-mode <模式>
```

使用這些引數時，`claude` 在 stdout 上為每個事件寫一行 JSON（工作階段開始、回覆的每個片段、每次工具呼叫、
每個權限請求、一輪對話結束），並從 stdin 為每個命令讀一行 JSON（使用者訊息、對權限請求的答覆、
切換模式或模型、中斷）。程式庫把這些行轉換為具型別的事件，把命令轉換為行。

## 安全性

對權限請求的答覆一律帶有處理程序一側從 `claude` 本身收到的 `input` 和 `permission_suggestions`。傳送命令的一方只能選擇請求、決定和訊息：答覆命令中沒有 `input` 欄位。向 `claude` 的 stdin 寫入的一方用它自己讀到的請求來建構答覆，每個請求只答覆一次。

如需回報漏洞，請參閱[安全性原則](SECURITY.en.md)（英文）。

## 藍圖

| 階段 | 產出 |
|---|---|
| 0 | 骨架：目錄結構、文件、CI 和網站 |
| 1 | .NET 程式庫：事件、剖析器、命令、工作階段和錄製重播 |
| 2 | 啟動 `claude` 的傳輸層、主控台範例，以及每週重新錄製 fixtures |
| 5 | 網站上的規格：通訊協定、命令、控制、傳輸、fixtures |
| 6 | Delphi 程式庫，通過同一套 fixtures |

第 3、4、7 階段在其他儲存庫中進行：以本程式庫為基礎的 Claude Visual Chat、使用它的應用程式，以及 VCL 聊天元件。
完整設計見 [docs/specs/](docs/specs/)（葡萄牙文）。

## 參與貢獻

請參閱 [CONTRIBUTING.en.md](CONTRIBUTING.en.md)（英文）。

## 授權條款

[MIT](LICENSE)
