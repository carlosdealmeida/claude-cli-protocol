using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace ClaudeCliProtocol
{
    /// <summary>
    /// A control request this session sent, with the claude's reply to it. It comes right after the
    /// <see cref="ControlAcknowledged"/> it carries.
    /// </summary>
    public sealed record ControlReplied(ControlCommand Command, ControlAcknowledged Reply) : ClaudeEvent;

    /// <summary>
    /// A conversation with a claude over a transport: it reads the transport's lines into typed events, sends the
    /// commands and keeps the state the lines tell (busy, pending permission requests, session id, confirmed mode
    /// and model). The state is updated before an event is handed out, so a reader sees the state that event left.
    /// <see cref="SendAsync"/> and the properties may be used from any thread.
    /// </summary>
    public sealed class ClaudeSession : IAsyncDisposable
    {
        private readonly object _gate = new object();
        private readonly IClaudeTransport _transport;
        private readonly StreamJsonParser _parser = new StreamJsonParser();
        private readonly PermissionLedger _ledger = new PermissionLedger();
        private readonly Dictionary<string, ControlCommand> _controls = new Dictionary<string, ControlCommand>();
        private int _turns;
        private string? _sessionId;
        private string? _permissionMode;
        private string? _model;
        private TransportState _transportState = TransportState.Open;
        private bool _reading;

        public ClaudeSession(IClaudeTransport transport)
        {
            _transport = transport ?? throw new ArgumentNullException(nameof(transport));
            _transport.StateChanged += OnTransportStateChanged;
        }

        /// <summary>The claude's id of the session, from its <c>system/init</c> line; null before the first one.</summary>
        public string? SessionId
        {
            get
            {
                lock (_gate) return _sessionId;
            }
        }

        /// <summary>
        /// The permission mode the claude confirmed: the one of its <c>system/init</c> or <c>system/status</c> line,
        /// or of a <see cref="SetPermissionMode"/> it answered with a success. Asking for a mode does not change it.
        /// </summary>
        public string? PermissionMode
        {
            get
            {
                lock (_gate) return _permissionMode;
            }
        }

        /// <summary>The model the claude confirmed, as <see cref="PermissionMode"/> for <see cref="SetModel"/>.</summary>
        public string? Model
        {
            get
            {
                lock (_gate) return _model;
            }
        }

        /// <summary>
        /// A turn is open: each message sent opens one and each <c>result</c> line closes one. A <c>system/init</c>
        /// that arrives with no turn open opens one too (a message that came from somewhere else, as through Remote
        /// Control).
        /// </summary>
        public bool IsBusy
        {
            get
            {
                lock (_gate) return _turns > 0;
            }
        }

        /// <summary>The permission requests waiting for an answer, in the order they arrived. A copy.</summary>
        public IReadOnlyList<PermissionRequested> PendingPermissions => _ledger.Pending;

        /// <summary>Where the transport stands.</summary>
        public TransportState TransportState
        {
            get
            {
                lock (_gate) return _transportState;
            }
        }

        /// <summary>Raised after any of the properties changed. It may come from the reading thread or from a sender's.</summary>
        public event EventHandler? StateChanged;

        /// <summary>
        /// The events of the session, until the transport closes. A line the parser does not understand gives no
        /// event and does not stop the reading. When the transport closes, the session is idle, with nothing pending.
        /// Call it once.
        /// </summary>
        public async IAsyncEnumerable<ClaudeEvent> ReadEventsAsync([EnumeratorCancellation] CancellationToken ct = default)
        {
            lock (_gate)
            {
                if (_reading) throw new InvalidOperationException("The events of a session are read only once.");
                _reading = true;
            }

            while (true)
            {
                var line = await _transport.ReceiveLineAsync(ct).ConfigureAwait(false);
                if (line == null)
                {
                    if (Closed()) StateChanged?.Invoke(this, EventArgs.Empty);
                    yield break;
                }
                foreach (var e in _parser.Parse(line))
                {
                    if (Apply(e, out var replied)) StateChanged?.Invoke(this, EventArgs.Empty);
                    yield return e;
                    if (replied != null) yield return replied;
                }
            }
        }

        /// <summary>
        /// Sends a command. A message opens a turn. A control command gets a new request id, and its reply comes back
        /// as a <see cref="ControlReplied"/>. An answer to a permission request goes only when the request is pending,
        /// and takes it out: false, and nothing sent, for a request already answered, cancelled, resolved or unknown.
        /// What the transport throws is thrown here.
        /// </summary>
        public async Task<bool> SendAsync(ClaudeCommand command, CancellationToken ct = default)
        {
            switch (command)
            {
                case null:
                    throw new ArgumentNullException(nameof(command));

                case AnswerPermission answer:
                    if (!_ledger.TryTake(answer.RequestId, out _)) return false;
                    StateChanged?.Invoke(this, EventArgs.Empty);
                    await _transport.SendAsync(answer, ct).ConfigureAwait(false);
                    return true;

                case ControlCommand control:
                    {
                        var sent = control with { RequestId = Guid.NewGuid().ToString("N") };
                        // kept before it is sent: the reply can arrive before the send returns
                        lock (_gate) _controls[sent.RequestId!] = sent;
                        try
                        {
                            await _transport.SendAsync(sent, ct).ConfigureAwait(false);
                        }
                        catch
                        {
                            lock (_gate) _controls.Remove(sent.RequestId!);
                            throw;
                        }
                        return true;
                    }

                case SendUserMessage message:
                    {
                        // opened before it is sent: a quick result must find its turn
                        bool changed;
                        lock (_gate) changed = _turns++ == 0;
                        if (changed) StateChanged?.Invoke(this, EventArgs.Empty);
                        try
                        {
                            await _transport.SendAsync(message, ct).ConfigureAwait(false);
                        }
                        catch
                        {
                            lock (_gate)
                            {
                                _turns = Math.Max(0, _turns - 1);
                                changed = _turns == 0;
                            }
                            if (changed) StateChanged?.Invoke(this, EventArgs.Empty);
                            throw;
                        }
                        return true;
                    }

                default:
                    await _transport.SendAsync(command, ct).ConfigureAwait(false);
                    return true;
            }
        }

        /// <summary>Stops following the transport and disposes it.</summary>
        public ValueTask DisposeAsync()
        {
            _transport.StateChanged -= OnTransportStateChanged;
            return _transport.DisposeAsync();
        }

        /// <summary>Updates the state with one event. True when a property changed.</summary>
        private bool Apply(ClaudeEvent e, out ControlReplied? replied)
        {
            replied = null;
            var changed = _ledger.Observe(e);
            lock (_gate)
            {
                var busy = _turns > 0;
                var before = (_sessionId, _permissionMode, _model);
                switch (e)
                {
                    case SessionStarted started:
                        if (!string.IsNullOrEmpty(started.SessionId)) _sessionId = started.SessionId;
                        if (!string.IsNullOrEmpty(started.Model)) _model = started.Model;
                        if (!string.IsNullOrEmpty(started.PermissionMode)) _permissionMode = started.PermissionMode;
                        if (_turns == 0) _turns = 1;
                        break;

                    case StatusChanged status:
                        if (!string.IsNullOrEmpty(status.PermissionMode)) _permissionMode = status.PermissionMode;
                        break;

                    case TurnCompleted _:
                        _turns = Math.Max(0, _turns - 1); // an interrupt with the session idle also brings a result
                        break;

                    case ControlAcknowledged reply:
                        if (!_controls.TryGetValue(reply.RequestId, out var command)) break;
                        _controls.Remove(reply.RequestId);
                        replied = new ControlReplied(command, reply);
                        if (reply.IsError) break;
                        switch (command)
                        {
                            case SetPermissionMode mode:
                                _permissionMode = mode.Mode;
                                break;
                            case SetModel model:
                                _model = model.Model;
                                break;
                            case Interrupt _:
                                // the claude may drop queued messages: only those still queued will bring a result,
                                // besides the interrupted turn itself
                                if (_turns > 0 && StillQueued(reply) is { } queued) _turns = queued + 1;
                                break;
                        }
                        break;
                }
                return changed || busy != (_turns > 0) || before != (_sessionId, _permissionMode, _model);
            }
        }

        private static int? StillQueued(ControlAcknowledged reply) =>
            reply.Response is { ValueKind: JsonValueKind.Object } body
            && body.TryGetProperty("still_queued", out var queue)
            && queue.ValueKind == JsonValueKind.Array
                ? queue.GetArrayLength()
                : (int?)null;

        /// <summary>The transport gave its last line. True when a property changed.</summary>
        private bool Closed()
        {
            var pending = _ledger.Pending.Count > 0;
            _ledger.Clear();
            lock (_gate)
            {
                var changed = pending || _turns > 0 || _transportState != TransportState.Closed;
                _turns = 0;
                _controls.Clear();
                _transportState = TransportState.Closed;
                return changed;
            }
        }

        private void OnTransportStateChanged(object? sender, TransportState state)
        {
            lock (_gate)
            {
                if (_transportState == state) return;
                _transportState = state;
            }
            StateChanged?.Invoke(this, EventArgs.Empty);
        }
    }
}
