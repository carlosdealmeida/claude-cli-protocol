using System;
using System.Collections.Generic;

namespace ClaudeCliProtocol
{
    /// <summary>
    /// The permission requests the claude sent and nobody answered yet. Whoever writes an answer to the claude's
    /// stdin takes the request from here (<see cref="TryTake"/>) and builds the answer from it
    /// (<see cref="StdinWriter.Write(AnswerPermission, PermissionRequested)"/>), so the tool's input and the
    /// permission suggestions are always the ones the claude sent. Taking removes the request: a second answer to
    /// the same request finds nothing. Thread-safe.
    /// </summary>
    public sealed class PermissionLedger
    {
        private readonly object _gate = new object();
        private readonly List<PermissionRequested> _pending = new List<PermissionRequested>();

        /// <summary>A copy, in the order the requests arrived.</summary>
        public IReadOnlyList<PermissionRequested> Pending
        {
            get
            {
                lock (_gate) return _pending.ToArray();
            }
        }

        /// <summary>
        /// Follows one event of the stream. A request enters; it leaves when the claude cancels it, when the result
        /// of its tool call arrives or when the turn ends. Returns true when the pending requests changed.
        /// </summary>
        public bool Observe(ClaudeEvent e)
        {
            lock (_gate)
            {
                switch (e)
                {
                    case PermissionRequested request:
                        {
                            var index = _pending.FindIndex(p => p.RequestId == request.RequestId);
                            if (index >= 0) _pending[index] = request;
                            else _pending.Add(request);
                            return true;
                        }
                    case PermissionCancelled cancelled:
                        return _pending.RemoveAll(p => p.RequestId == cancelled.RequestId) > 0;
                    case UserBlock { Block: ToolResultBlock result }:
                        return _pending.RemoveAll(p => p.ToolUseId != null && p.ToolUseId == result.ToolUseId) > 0;
                    case TurnCompleted _:
                        {
                            var any = _pending.Count > 0;
                            _pending.Clear();
                            return any;
                        }
                    default:
                        return false;
                }
            }
        }

        /// <summary>
        /// Takes a request out. False, and <paramref name="request"/> null, when it is not pending: already
        /// answered, cancelled, resolved or unknown.
        /// </summary>
        public bool TryTake(string requestId, out PermissionRequested? request)
        {
            lock (_gate)
            {
                var index = requestId == null ? -1 : _pending.FindIndex(p => p.RequestId == requestId);
                if (index < 0)
                {
                    request = null;
                    return false;
                }
                request = _pending[index];
                _pending.RemoveAt(index);
                return true;
            }
        }

        /// <summary>Forgets every request: the process ended or the transport closed.</summary>
        public void Clear()
        {
            lock (_gate) _pending.Clear();
        }
    }
}
