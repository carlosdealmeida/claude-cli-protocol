---
title: 传输层
description: 负责传递行和命令的部分——接口、claude 进程、参数及其校验、启动、结束和已知限制。
---

:::note
本页由 AI 翻译，尚未经母语者审校。术语见 [docs/i18n/glossary.md](https://github.com/carlosdealmeida/claude-cli-protocol/blob/master/docs/i18n/glossary.md)。
:::

传输层负责把 `claude` 的行朝一个方向传递，把命令朝另一个方向传递。它可以是进程本身、通往远程 `claude`
的网络，或一份录制。本页说明传输层做什么，并详细说明在本机启动 `claude` 的那一个（.NET 库的
`ProcessTransport`）做什么。这里作为规则写出的是库的规则；属于 `claude` 的行为会单独说明。

## 接口

接口面向命令，而不是原始的 stdin：

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

- **行向上，命令向下。** 使用传输层的一方逐行接收 stdout 的行，并发送命令：一条消息、一个权限回复、
  一个控制请求。把命令的行写入 stdin 的是进程一侧。
- **为什么用命令。** 权限回复由进程一侧用它读到的请求来组装（见[规则](../controle/#权限回复的规则)）。
  如果是原始 stdin，调用方可以写入任意 `input`。
- **状态：** `Open`（行进来，命令出去）、`Dropped`（断开了，可能恢复：会重连的网络传输层）和
  `Closed`（已结束：不会再有行，每次读取都返回 null）。
- **库中的传输层：** `ProcessTransport` 启动 `claude`；`ReplayTransport` 播放一份录制并忽略命令。网络
  传输层属于使用它的应用，而不属于库。

## 进程

### 参数

按这个顺序，且不加其他参数：

```
-p --input-format stream-json --output-format stream-json --verbose
--include-partial-messages --permission-prompt-tool stdio --permission-mode <modo>
[--resume <id>] [--model <modelo>] [--effort <nível>]
```

后三个只在请求时才加。**绝不使用 `--setting-sources`：** 每台机器的拦截规则在任何模式下都有效。

### 校验

在 Windows 上，通过 npm 安装的 `claude` 是一个 `claude.cmd`，会经过 `cmd.exe`。在那里，值中一个多余的
字符就会变成另一条命令。所以每个进入命令行的值都要校验，被拒绝的值不会启动任何东西：

| 值 | 接受 |
|---|---|
| 模式 | `plan`、`default`、`acceptEdits` 或 `auto` |
| 模型 | `^[A-Za-z0-9][A-Za-z0-9._:@/\[\]-]{0,99}$`：别名（`sonnet`、`opus[1m]`）、id、Bedrock 配置（`arn:…`）和 Vertex id（`…@20250514`） |
| 会话 id | `^[A-Za-z0-9][A-Za-z0-9_-]{0,199}$` |
| effort | `low`、`medium`、`high`、`xhigh` 或 `max` |
| 文件夹 | 非空 |
| `claude` 的路径 | 给出时必须是绝对路径 |

运行中请求切换模式时也一样：模式不在列表中的 `set_permission_mode` 不会被写出。校验会拦下的值举例：
模型 `sonnet & calc`、会话 id `a" --dangerously-skip-permissions`、模式 `bypassPermissions`。

### 查找 `claude`

没有给出路径时，传输层在 PATH 中查找 `claude`，**只在其中的绝对路径文件夹里找，绝不在当前文件夹里找**：
当前文件夹就是对话的文件夹，留在那里的 `claude.cmd` 不能被当作要运行的那个。

### 环境

- 本机的环境，加上宿主为这个 `claude` 传入的变量（值为 null 的变量会被删除）。其中包括选择账户的
  `CLAUDE_CONFIG_DIR`。传输层不按名称识别任何变量。
- 再加上 **`NoDefaultCurrentDirectoryInExePath=1`**，让 `cmd.exe` 不在对话的文件夹里查找 `node`。
- 账户是本机的：订阅登录或 `ANTHROPIC_API_KEY`。把聊天嵌入第三方产品的人应使用 API key。

### stdin、stdout 和 stderr

- **stdin：** 每条命令一行，ASCII 字节，带 `\n` 并 flush（见[命令](../comandos/#编码)）。开始写的一行会
  写完：半行会破坏流。传输层从不自行关闭 stdin。
- **stdout：** UTF-8，逐行读取。
- **stderr：** 读取后丢弃。必须读取，否则管道会写满，`claude` 会卡住；但不保存，因为其中可能有对话的
  文本。库只统计行数。
- **两路读取同时进行，各自在自己的线程上。** 先把 stdout 读完再读 stderr 会让两边都卡住。在库中，
  每路读取都有自己的线程：如果用线程池的线程，每个运行中的 `claude` 会在整个生命周期内占住其中三个，
  在 4 核机器上两个传输层就足以让程序的其他部分等待。

## 启动

1. 校验各个值并组装参数。
2. 在对话的文件夹中启动进程。
3. **开始读取 stdout 和 stderr。**
4. **写入 `initialize`**，与官方 SDK 一样（可以关闭）。

第 3 步和第 4 步的顺序很重要。测试中发现：在 `netstandard2.0` 构建、.NET Framework 下，如果 `claude` 在
读取 stdin 之前写了很多内容，`initialize` 的写入不会完成；只有有人读取 stdout 后它才会完成。确切原因
尚未查明。

- **延迟。** 从 2.1.323 起有报告称到第一个字节需要 5 到 7 秒
  （[#109534](https://github.com/anthropics/claude-code/issues/109534)）；2.1.325 开始在第一个请求之前
  等待账户数据，2.1.327 最多等待 10 秒获取模型设置。启动时限太短会失败。
- **启动失败**会关闭传输层：读取返回 null，退出码可用。从 2.1.314 起，无法启动的 `claude -p` 会在
  stderr 上打印错误并以 1 退出（之前以 0 退出且没有输出）。由于 stderr 被丢弃，退出码是剩下的唯一信号。
- **在启动过程中释放**不会留下进程：传输层在启动 `claude` 后再次检查，如果已被释放，就结束整个进程树。

## 忙碌

`ClaudeSession` 会说明会话是否忙碌。规则跟随流，而不是消息计数：

> 从发送一条消息或收到一个 `system/init` 起，直到下一个 `result`，会话处于忙碌状态。

按轮计数（每条消息开启一轮，每个 `result` 结束一轮）是对不上的：在 `queued` 场景中，两条消息只产生了
一轮（见[协议](../protocolo/#一轮对话)）。这条规则的代价是：排队等待的消息会让会话在上一轮的 `result`
和它自己的 `system/init` 之间短暂空闲。

会话还保存已确认的 `session_id`、模式和模型（来自 `system/init`、`system/status` 以及请求的成功回复），
以及待处理的权限请求。传输层关闭后发送的命令不会被发送，不会让会话变为忙碌，并返回 `false`。

## 结束

- **结束来自进程，而不是 stdout 的结束。** 继承了 stdout 的 `claude` 子进程可能让它一直保持打开。进程
  结束后，传输层在有限时间内（库中为 10 秒）等待最后几行（即 `result`），然后关闭。
- **退出码**在结束后可用。
- **结束就是立即杀掉整个进程树。** 不先尝试温和地结束：`claude` 自行退出后，它的子进程就无法再通过它的
  pid 找到，等待会留下进程。代价是：如果宿主在一轮对话中途释放它，`claude` 不会完成写入磁盘的操作。
- **结束之后，发送什么也不做：** 不写入，也不抛出管道异常。

## 已知限制

- 在 Windows 以外的 `netstandard2.0` 上，结束时只杀掉 `claude`，不杀它的子进程。
- 在自行退出的 `claude` 之后仍存活的子进程不会被结束。
- `ClaudeLocator` 不检查可执行位。
- 当孤儿子进程占着 stdout 时，读取线程会继续存活，但结束之后不会再交出任何行。
- 针对真实的 `claude`（2.1.331，Windows），传输层只运行过一次，发送了一条简短消息，该测试仅在明确要求时
  运行（`CCP_LIVE_CLAUDE=1`）。其余测试使用模拟的 `claude`。

## 传输层绝不做的事

- 把对话、stderr 或凭据写入磁盘。
- 传入列表之外的参数，或未经校验的值。
- 用不是自己从 `claude` 读到的 `input` 写权限回复。
- 在有待处理消息时关闭 stdin。
