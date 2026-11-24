using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace ClaudeCliProtocol
{
    /// <summary>
    /// Plays a recording: gives its lines in order, without waiting between them, and ignores every command. Blank
    /// lines are skipped and a carriage return at the end of a line is removed. For tests, for conformance and for
    /// samples that show a session without a claude.
    /// </summary>
    public sealed class ReplayTransport : IClaudeTransport
    {
        private readonly object _gate = new object();
        private readonly IEnumerator<string> _lines;
        private bool _closed;

        public ReplayTransport(IEnumerable<string> lines)
        {
            if (lines == null) throw new ArgumentNullException(nameof(lines));
            _lines = lines.GetEnumerator();
        }

        /// <summary>The recording in a file, read whole as UTF-8: one line of the stream per line of the file.</summary>
        public static ReplayTransport FromFile(string path)
        {
            if (path == null) throw new ArgumentNullException(nameof(path));
            return new ReplayTransport(File.ReadAllLines(path, Encoding.UTF8));
        }

        public event EventHandler<TransportState>? StateChanged;

        public Task<string?> ReceiveLineAsync(CancellationToken ct)
        {
            ct.ThrowIfCancellationRequested();
            string? next = null;
            var justClosed = false;
            lock (_gate)
            {
                while (!_closed)
                {
                    if (!_lines.MoveNext())
                    {
                        justClosed = CloseLocked();
                        break;
                    }
                    var line = _lines.Current?.TrimEnd('\r');
                    if (string.IsNullOrWhiteSpace(line)) continue;
                    next = line;
                    break;
                }
            }
            if (justClosed) StateChanged?.Invoke(this, TransportState.Closed);
            return Task.FromResult(next);
        }

        /// <summary>Does nothing: a recording takes no commands.</summary>
        public Task SendAsync(ClaudeCommand command, CancellationToken ct) => Task.CompletedTask;

        public ValueTask DisposeAsync()
        {
            bool justClosed;
            lock (_gate) justClosed = CloseLocked();
            if (justClosed) StateChanged?.Invoke(this, TransportState.Closed);
            return default;
        }

        private bool CloseLocked()
        {
            if (_closed) return false;
            _closed = true;
            _lines.Dispose();
            return true;
        }
    }
}
