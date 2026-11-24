using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace ClaudeCliProtocol.Tests
{
    /// <summary>
    /// A transport a test drives by hand: it gives the lines the test queues and keeps the commands it was sent.
    /// </summary>
    internal sealed class ScriptedTransport : IClaudeTransport
    {
        private readonly object _gate = new object();
        private readonly Queue<string> _lines = new Queue<string>();
        private TaskCompletionSource<string?>? _waiter;
        private bool _closed;
        private bool _reportedClosed;

        /// <summary>The commands received, in order.</summary>
        public List<ClaudeCommand> Sent { get; } = new List<ClaudeCommand>();

        /// <summary>Called inside <see cref="SendAsync"/>, before it returns.</summary>
        public Action<ClaudeCommand>? OnSend { get; set; }

        /// <summary>Makes the next <see cref="SendAsync"/> throw, as a broken pipe would.</summary>
        public bool FailNextSend { get; set; }

        public event EventHandler<TransportState>? StateChanged;

        public void Enqueue(string line)
        {
            TaskCompletionSource<string?>? waiter = null;
            lock (_gate)
            {
                if (_waiter != null)
                {
                    waiter = _waiter;
                    _waiter = null;
                }
                else
                {
                    _lines.Enqueue(line);
                }
            }
            waiter?.SetResult(line);
        }

        /// <summary>
        /// After the queued lines, the transport gives null. It reports Closed when that null is handed out, as a
        /// real transport does: its last lines come before its end.
        /// </summary>
        public void Close()
        {
            TaskCompletionSource<string?>? waiter;
            lock (_gate)
            {
                if (_closed) return;
                _closed = true;
                waiter = _waiter;
                _waiter = null;
            }
            if (waiter == null) return;
            ReportClosed();
            waiter.SetResult(null);
        }

        private void ReportClosed()
        {
            lock (_gate)
            {
                if (_reportedClosed) return;
                _reportedClosed = true;
            }
            StateChanged?.Invoke(this, TransportState.Closed);
        }

        public void Raise(TransportState state) => StateChanged?.Invoke(this, state);

        /// <summary>True while a read waits for a line: everything queued so far was handed out and handled.</summary>
        public bool ReaderIsWaiting
        {
            get
            {
                lock (_gate) return _waiter != null;
            }
        }

        public void WaitForReader()
        {
            if (!SpinWait.SpinUntil(() => ReaderIsWaiting, TimeSpan.FromSeconds(10)))
                throw new TimeoutException("The reader did not come back for the next line.");
        }

        /// <summary>Makes the next <see cref="ReceiveLineAsync"/> throw, as a pipe that broke would.</summary>
        public bool FailNextReceive { get; set; }

        public Task<string?> ReceiveLineAsync(CancellationToken ct)
        {
            ct.ThrowIfCancellationRequested();
            if (FailNextReceive)
            {
                FailNextReceive = false;
                throw new IOException("The pipe is broken.");
            }
            lock (_gate)
            {
                if (_lines.Count > 0) return Task.FromResult<string?>(_lines.Dequeue());
                if (!_closed)
                {
                    _waiter = new TaskCompletionSource<string?>(TaskCreationOptions.RunContinuationsAsynchronously);
                    return _waiter.Task;
                }
            }
            ReportClosed();
            return Task.FromResult<string?>(null);
        }

        public Task SendAsync(ClaudeCommand command, CancellationToken ct)
        {
            if (FailNextSend)
            {
                FailNextSend = false;
                throw new IOException("The pipe is broken.");
            }
            lock (_gate) Sent.Add(command);
            OnSend?.Invoke(command);
            return Task.CompletedTask;
        }

        public ValueTask DisposeAsync()
        {
            Close();
            return default;
        }
    }
}
