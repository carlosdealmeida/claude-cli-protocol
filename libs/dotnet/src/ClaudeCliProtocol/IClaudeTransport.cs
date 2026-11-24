using System;
using System.Threading;
using System.Threading.Tasks;

namespace ClaudeCliProtocol
{
    /// <summary>Where a transport stands.</summary>
    public enum TransportState
    {
        /// <summary>Lines come and commands go.</summary>
        Open,

        /// <summary>The transport lost its connection and may come back (a network transport that reconnects).</summary>
        Dropped,

        /// <summary>The transport ended: no more lines will come.</summary>
        Closed,
    }

    /// <summary>
    /// What carries the claude's lines one way and the commands the other: a process, a network, a recording. The
    /// interface is of commands, not of raw stdin: the side that holds the process is the one that writes a command
    /// to it, so it can answer a permission request with the input the claude itself sent
    /// (<see cref="PermissionLedger"/>).
    /// </summary>
    public interface IClaudeTransport : IAsyncDisposable
    {
        /// <summary>The next stream-json line, or null once the transport has closed.</summary>
        Task<string?> ReceiveLineAsync(CancellationToken ct);

        /// <summary>A command; each transport delivers it in its own way.</summary>
        Task SendAsync(ClaudeCommand command, CancellationToken ct);

        /// <summary>Raised when the transport drops, comes back or closes.</summary>
        event EventHandler<TransportState>? StateChanged;
    }
}
