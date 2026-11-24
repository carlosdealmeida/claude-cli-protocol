using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
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
        private bool _busy;
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
        /// The claude is working, or about to: true from a message sent, or from a <c>system/init</c> line (the start
        /// of a turn, also of one this side did not ask for, as a message through Remote Control), until the next
        /// <c>result</c> line. The stream decides, not a count of the messages sent: the claude may take a message
        /// sent while it works into the running turn, and then one <c>result</c> ends both. A message that waited
        /// for its own turn makes the session busy again at that turn's <c>system/init</c>, so between the two the
        /// session is idle for a moment.
        /// </summary>
        public bool IsBusy
        {
            get
            {
                lock (_gate) return _busy;
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
        /// Sends a command. A message makes the session busy. A control command gets a new request id, and its reply
        /// comes back as a <see cref="ControlReplied"/>. An answer to a permission request goes only when the request
        /// is pending, and takes it out: false, and nothing sent, for a request already answered, cancelled, resolved
        /// or unknown. True means the command was handed to the transport, not that the claude took it. What the
        /// transport throws is thrown here; an answer the transport failed to deliver stays taken out, because nobody
        /// knows whether the claude got it, and after that only an <see cref="Interrupt"/> frees the claude.
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
                        // busy before it is sent: a quick result must find the session busy
                        bool wasIdle;
                        lock (_gate)
                        {
                            wasIdle = !_busy;
                            _busy = true;
                        }
                        if (wasIdle) StateChanged?.Invoke(this, EventArgs.Empty);
                        try
                        {
                            await _transport.SendAsync(message, ct).ConfigureAwait(false);
                        }
                        catch
                        {
                            // only what this send changed is undone: a turn that was already running goes on
                            if (wasIdle)
                            {
                                lock (_gate) _busy = false;
                                StateChanged?.Invoke(this, EventArgs.Empty);
                            }
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
                var busy = _busy;
                var before = (_sessionId, _permissionMode, _model);
                switch (e)
                {
                    case SessionStarted started:
                        if (!string.IsNullOrEmpty(started.SessionId)) _sessionId = started.SessionId;
                        if (!string.IsNullOrEmpty(started.Model)) _model = started.Model;
                        if (!string.IsNullOrEmpty(started.PermissionMode)) _permissionMode = started.PermissionMode;
                        _busy = true;
                        break;

                    case StatusChanged status:
                        if (!string.IsNullOrEmpty(status.PermissionMode)) _permissionMode = status.PermissionMode;
                        break;

                    case TurnCompleted _:
                        _busy = false; // also for a result with no turn running: an interrupt of an idle session brings one
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
                        }
                        break;
                }
                return changed || busy != _busy || before != (_sessionId, _permissionMode, _model);
            }
        }

        /// <summary>The transport gave its last line. True when a property changed.</summary>
        private bool Closed()
        {
            var pending = _ledger.Pending.Count > 0;
            _ledger.Clear();
            lock (_gate)
            {
                var changed = pending || _busy || _transportState != TransportState.Closed;
                _busy = false;
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
