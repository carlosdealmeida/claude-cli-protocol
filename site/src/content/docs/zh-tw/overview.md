---
title: 概觀
description: claude-cli-protocol 是什麼、如何分層，以及目前處於哪個階段。
---

[Claude Code](https://github.com/anthropics/claude-code) 以 `-p` 執行時，每則訊息是一行 JSON。
**claude-cli-protocol** 把目前分散的三樣東西放在一起：

- 這個通訊協定的書面**規格**；
- 用來驗證規格的真實工作階段**錄製**；
- 實作該通訊協定的**程式庫**，先是 .NET，接著是 Delphi。

社群專案，與 Anthropic 無隸屬關係，亦未獲其認可。

## 狀態

**版本 0.1.0，2026-11-24。** .NET 程式庫已發佈到 NuGet：`dotnet add package ClaudeCliProtocol`（[nuget.org](https://www.nuget.org/packages/ClaudeCliProtocol)）。它能讀寫該通訊協定、啟動本機的 `claude -p`，並通過了儲存庫中的 50 份錄製。傳輸層使用模擬的 `claude` 進行測試；對真實的 `claude` 它執行過一次，傳送了一則簡短訊息，該測試僅在明確要求時執行。

## 分層

```
claude-cli-protocol（程式庫）  ← 只負責通訊協定；不知道任何介面
   ↑
ClaudeVisualChat.Avalonia      ← 只負責繪製；不啟動處理程序，也不存取網路
   ↑
你的應用程式                   ← 負責整合
```

每一層只認識它下面的一層，並透過帶版本號的套件相依。本儲存庫不包含任何介面。

## 如何呼叫 claude -p

```
claude -p --input-format stream-json --output-format stream-json --verbose
       --include-partial-messages --permission-prompt-tool stdio --permission-mode <模式>
```

`claude` 在 stdout 上為每個事件寫一行 JSON，並從 stdin 為每個命令讀一行 JSON。

## 階段

| 階段 | 產出 |
|---|---|
| 0 | 骨架：目錄結構、文件、CI 和網站 |
| 1 | .NET 程式庫：事件、剖析器、命令、工作階段和錄製重播 |
| 2 | 啟動 `claude` 的傳輸層、主控台範例，以及每週重新錄製 fixtures |
| 5 | 本網站上的規格：通訊協定、命令、控制、傳輸、fixtures |
| 6 | Delphi 程式庫，通過同一套 fixtures |

第 3、4、7 階段在其他儲存庫中進行。
