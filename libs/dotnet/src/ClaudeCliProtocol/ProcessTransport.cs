using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace ClaudeCliProtocol
{
    /// <summary>
    /// The transport that runs the claude on this machine: it starts <c>claude -p</c> in stream-json with checked
    /// arguments (<see cref="ClaudeArguments"/>), gives the lines of its stdout and writes the commands to its stdin.
    /// The stderr is read and dropped, because it may hold text of the conversation; reading it is needed, or the
    /// pipe fills and the claude stops. Nothing is written to disk.
    /// <para>
    /// This is where the permission answer rule is kept. The transport has a <see cref="PermissionLedger"/> of its
    /// own, fed only with the lines it read itself from the claude's stdout, and an <see cref="AnswerPermission"/>
    /// is written from the request that ledger holds: the tool's input and the permission suggestions are always the
    /// ones the claude sent, and a request is answered once.
    /// </para>
    /// </summary>
    public sealed class ProcessTransport : IClaudeTransport
    {
        private readonly ProcessTransportOptions _options;
        private readonly IReadOnlyList<string> _arguments;
        private readonly object _gate = new object();
        private readonly ConcurrentQueue<string> _lines = new ConcurrentQueue<string>();
        private readonly SemaphoreSlim _available = new SemaphoreSlim(0);
        private readonly SemaphoreSlim _writing = new SemaphoreSlim(1, 1);
        private readonly StreamJsonParser _parser = new StreamJsonParser();
        private readonly PermissionLedger _ledger = new PermissionLedger();
        private readonly TaskCompletionSource<bool> _ended =
            new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        private Process? _process;
        private Stream? _stdin;
        private Task _stdoutReader = Task.CompletedTask;
        private Task _stderrReader = Task.CompletedTask;
        private int _stderrLines;
        private int? _exitCode;
        private bool _started;
        private bool _completed;
        private bool _disposed;

        /// <summary>
        /// Checks the options at once: a refused value throws <see cref="ArgumentException"/> here. So does a
        /// <see cref="ProcessTransportOptions.ClaudePath"/> that is not absolute: a bare name would be looked for in
        /// the current folder.
        /// </summary>
        public ProcessTransport(ProcessTransportOptions options)
        {
            _options = options ?? throw new ArgumentNullException(nameof(options));
            _arguments = ClaudeArguments.Build(options);
            if (options.ClaudePath != null
                && !ClaudeLocator.IsAbsolute(options.ClaudePath, RuntimeInformation.IsOSPlatform(OSPlatform.Windows)))
                throw new ArgumentException("The path of the claude must be absolute.", nameof(options));
        }

        /// <summary>The exit code, once the process has ended; null before. It is the only sign of a start that failed.</summary>
        public int? ExitCode
        {
            get
            {
                lock (_gate) return _exitCode;
            }
        }

        /// <summary>How many lines the claude wrote to stderr. The lines themselves are read and dropped.</summary>
        public int StderrLines => Volatile.Read(ref _stderrLines);

        /// <summary>
        /// After the process ends, how long the last lines of stdout are waited for: a child of the claude that
        /// inherited its stdout may hold it open for ever.
        /// </summary>
        internal TimeSpan DrainTimeout { get; set; } = TimeSpan.FromSeconds(10);

        /// <summary>For the tests: runs right before the process is started.</summary>
        internal Action? BeforeSpawn { get; set; }

        /// <summary>For the tests: the id of the process this transport started; null when it started none.</summary>
        internal int? SpawnedProcessId { get; private set; }

        /// <summary>
        /// Open when the claude has started, Closed when it has ended. An exception a subscriber throws is dropped.
        /// </summary>
        public event EventHandler<TransportState>? StateChanged;

        /// <summary>
        /// Starts the claude. <see cref="FileNotFoundException"/> when there is no claude;
        /// <see cref="DirectoryNotFoundException"/> when the working directory does not exist;
        /// <see cref="System.ComponentModel.Win32Exception"/> when the system refuses to run the file;
        /// <see cref="ObjectDisposedException"/> when the transport was disposed, also while it started. Call it
        /// once: after a start that failed the transport is closed, and a read gives null.
        /// </summary>
        public void Start()
        {
            lock (_gate)
            {
                if (_disposed) throw new ObjectDisposedException(nameof(ProcessTransport));
                if (_started) throw new InvalidOperationException("The transport was already started.");
                _started = true;
            }

            Process process;
            TaskCompletionSource<bool> exited;
            try
            {
                process = Spawn(out exited);
            }
            catch
            {
                Complete(null); // nothing will ever come: a read must not wait for a claude that never started
                throw;
            }

            bool disposed;
            lock (_gate)
            {
                disposed = _disposed;
                if (!disposed)
                {
                    _process = process;
                    _stdin = process.StandardInput.BaseStream;
                }
            }
            if (disposed)
            {
                // disposed while the claude was starting: the dispose found no process, so it is ended here
                KillTree(process);
                process.Dispose();
                throw new ObjectDisposedException(nameof(ProcessTransport));
            }

            // The handshake the official SDKs send first. The turn to write is taken here, so nothing else can be
            // written before it; the write itself is not waited for. A claude that writes before it reads its
            // stdin must not hold Start, and its stdout has to be read from the first moment.
            var handshake = _options.SendInitialize ? StdinWriter.Write(new Initialize { RequestId = NewRequestId() }) : null;
            if (handshake != null) _writing.Wait();

            // The two reads block for as long as the claude lives. Each has a thread of its own: on threads of the
            // pool, a few claudes running would leave the application with none.
            _stdoutReader = OnItsOwnThread("claude stdout", () => ReadStdout(process));
            _stderrReader = OnItsOwnThread("claude stderr", () => ReadStderr(process));
            if (handshake != null) _ = Task.Run(() => WriteHeldAsync(handshake));

            // Open is told with the handshake already ahead of whatever a subscriber sends, and before the end is
            // watched for, so Closed never comes first.
            Raise(TransportState.Open);
            _ = WatchAsync(process, exited.Task);
        }

        /// <summary>Starts the process. Nothing of this transport knows it yet.</summary>
        private Process Spawn(out TaskCompletionSource<bool> exited)
        {
            var claude = _options.ClaudePath;
            if (claude == null)
                claude = ClaudeLocator.Find() ?? throw new FileNotFoundException("claude is not on the PATH of this machine.");
            else if (!File.Exists(claude))
                throw new FileNotFoundException("The claude executable was not found.", claude);
            if (!Directory.Exists(_options.WorkingDirectory))
                throw new DirectoryNotFoundException("The working directory does not exist: " + _options.WorkingDirectory);

            var info = new ProcessStartInfo(claude)
            {
                // every argument was checked against a closed set of characters: none needs quoting
                Arguments = string.Join(" ", _arguments),
                WorkingDirectory = _options.WorkingDirectory,
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardInput = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                StandardOutputEncoding = Encoding.UTF8,
                StandardErrorEncoding = Encoding.UTF8,
            };
            // the npm claude.cmd calls `node` by name: without this, cmd.exe looks first in the current folder,
            // which is the conversation's folder, and a node.cmd planted there would run instead
            info.Environment["NoDefaultCurrentDirectoryInExePath"] = "1";
            foreach (var variable in _options.Environment)
            {
                if (variable.Value == null) info.Environment.Remove(variable.Key);
                else info.Environment[variable.Key] = variable.Value;
            }

            var process = new Process { StartInfo = info, EnableRaisingEvents = true };
            var ended = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
            process.Exited += (_, __) => ended.TrySetResult(true); // told by the system: no thread waits for it
            exited = ended;
            BeforeSpawn?.Invoke();
            process.Start();
            SpawnedProcessId = process.Id;
            return process;
        }

        /// <summary>
        /// Tells the subscribers, one by one. What a subscriber throws is dropped: it must stop neither the start
        /// nor the end of the transport, nor keep the other subscribers from hearing.
        /// </summary>
        private void Raise(TransportState state)
        {
            var handlers = StateChanged;
            if (handlers == null) return;
            foreach (var handler in handlers.GetInvocationList())
            {
                try
                {
                    ((EventHandler<TransportState>)handler)(this, state);
                }
                catch (Exception)
                {
                }
            }
        }

        /// <summary>The next line of the claude's stdout, or null once the claude has ended. Blank lines are skipped.</summary>
        public async Task<string?> ReceiveLineAsync(CancellationToken ct)
        {
            lock (_gate)
            {
                if (!_started && !_disposed) throw new InvalidOperationException("The transport was not started.");
            }
            while (true)
            {
                await _available.WaitAsync(ct).ConfigureAwait(false);
                if (_lines.TryDequeue(out var line)) return line;
                _available.Release(); // the end: stay signalled, so every later read also gives null
                return null;
            }
        }

        /// <summary>
        /// Writes a command to the claude's stdin, one whole line at a time. A control command with no request id
        /// gets one. An answer to a permission request is written from the request this transport read from the
        /// claude; when that request is not pending here (already answered, cancelled, resolved or never seen),
        /// nothing is written. After the claude has ended, nothing is written and nothing is thrown. A
        /// <see cref="SetPermissionMode"/> with a mode outside <see cref="ClaudeArguments.PermissionModes"/> throws
        /// <see cref="ArgumentException"/> and writes nothing. The token cancels the wait for the turn to write,
        /// which lasts while a claude that stopped reading holds an earlier write; a write that began is finished,
        /// because half a line would break the stream.
        /// </summary>
        public async Task SendAsync(ClaudeCommand command, CancellationToken ct)
        {
            if (command == null) throw new ArgumentNullException(nameof(command));
            lock (_gate)
            {
                if (!_started && !_disposed) throw new InvalidOperationException("The transport was not started.");
            }
            ct.ThrowIfCancellationRequested();

            string? line;
            switch (command)
            {
                case SendUserMessage message:
                    line = StdinWriter.Write(message);
                    break;
                case SetPermissionMode mode when !ClaudeArguments.IsPermissionMode(mode.Mode):
                    throw new ArgumentException(
                        "Permission mode refused; accepted: " + string.Join(", ", ClaudeArguments.PermissionModes) + ".", nameof(command));
                case ControlCommand control:
                    line = StdinWriter.Write(control.RequestId == null ? control with { RequestId = NewRequestId() } : control);
                    break;
                case AnswerPermission answer:
                    line = _ledger.TryTake(answer.RequestId, out var request) ? StdinWriter.Write(answer, request!) : null;
                    break;
                default:
                    throw new NotSupportedException("This transport cannot write a " + command.GetType().Name + ".");
            }
            if (line != null) await SendRawAsync(line, ct).ConfigureAwait(false);
        }

        private static string NewRequestId() => Guid.NewGuid().ToString("N");

        /// <summary>One line to the claude's stdin, as ASCII bytes and a line feed. False when the process has ended.</summary>
        internal async Task<bool> SendRawAsync(string line, CancellationToken ct = default)
        {
            await _writing.WaitAsync(ct).ConfigureAwait(false);
            return await WriteHeldAsync(line).ConfigureAwait(false);
        }

        /// <summary>Writes one line with the write lock already held, and releases the lock.</summary>
        private async Task<bool> WriteHeldAsync(string line)
        {
            var bytes = Encoding.ASCII.GetBytes(line + "\n");
            try
            {
                Stream? stdin;
                lock (_gate) stdin = _completed ? null : _stdin;
                if (stdin == null) return false;
                await stdin.WriteAsync(bytes, 0, bytes.Length).ConfigureAwait(false);
                await stdin.FlushAsync().ConfigureAwait(false);
                return true;
            }
            catch (Exception e) when (e is IOException || e is ObjectDisposedException || e is InvalidOperationException)
            {
                return false; // a broken or closed pipe: the process has ended
            }
            finally
            {
                _writing.Release();
            }
        }

        /// <summary>Ends the claude and its children at once, then waits for the end to be told.</summary>
        public async ValueTask DisposeAsync()
        {
            Process? process;
            lock (_gate)
            {
                if (_disposed) return;
                _disposed = true;
                process = _process;
            }
            if (process == null)
            {
                Complete(null);
                return;
            }

            KillTree(process);
            await Task.WhenAny(_ended.Task, Task.Delay(TimeSpan.FromSeconds(15))).ConfigureAwait(false);
            Complete(null);
            process.Dispose();
        }

        /// <summary>
        /// Runs a read on a background thread of its own. The task ends when the read ends, however it ends: an
        /// exception stays here, as it stayed in the task before, and never takes the application down.
        /// </summary>
        private static Task OnItsOwnThread(string name, Action read)
        {
            var done = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
            var thread = new Thread(() =>
            {
                try
                {
                    read();
                }
                catch (Exception)
                {
                    // the end is told by the process, not by this thread
                }
                finally
                {
                    done.TrySetResult(true);
                }
            })
            {
                IsBackground = true,
                Name = name,
            };
            thread.Start();
            return done.Task;
        }

        private void ReadStdout(Process process)
        {
            try
            {
                string? line;
                while ((line = process.StandardOutput.ReadLine()) != null)
                {
                    line = line.TrimEnd('\r');
                    if (line.Trim().Length == 0) continue;
                    Observe(line);
                    lock (_gate)
                    {
                        if (_completed) return; // the end was told: nothing comes after it
                        _lines.Enqueue(line);
                    }
                    _available.Release();
                }
            }
            catch (Exception e) when (e is IOException || e is ObjectDisposedException || e is InvalidOperationException)
            {
                // the pipe went away with the process
            }
        }

        /// <summary>
        /// What the transport itself takes from a line, before handing it out: the permission requests go to the
        /// ledger, and a control request nobody here can answer is answered with an error.
        /// </summary>
        private void Observe(string line)
        {
            var asked = false;
            foreach (var e in _parser.Parse(line))
            {
                _ledger.Observe(e);
                if (e is PermissionRequested) asked = true;
            }

            if (line.IndexOf("control_request", StringComparison.Ordinal) < 0) return;
            var id = ControlRequestId(line, out var isPermission);
            if (id == null) return;
            // A hook, an MCP message, an elicitation..., or a permission request the parser could not read, which
            // nobody will ever see: without an answer the claude would wait for ever.
            // Not awaited here: the write may wait on the pipe, and this thread is the one that keeps stdout moving.
            if (!isPermission) _ = SendRawAsync(StdinWriter.WriteControlError(id, "not supported"));
            else if (!asked) _ = SendRawAsync(StdinWriter.WriteControlError(id, "malformed permission request"));
        }

        /// <summary>The request id of a control request, and whether it asks a permission; null for any other line.</summary>
        private static string? ControlRequestId(string line, out bool isPermission)
        {
            isPermission = false;
            try
            {
                using (var doc = JsonDocument.Parse(line))
                {
                    var root = doc.RootElement;
                    if (root.ValueKind != JsonValueKind.Object
                        || !root.TryGetProperty("type", out var type) || type.ValueKind != JsonValueKind.String
                        || type.GetString() != "control_request"
                        || !root.TryGetProperty("request_id", out var id) || id.ValueKind != JsonValueKind.String
                        || string.IsNullOrEmpty(id.GetString()))
                        return null;
                    isPermission = root.TryGetProperty("request", out var request) && request.ValueKind == JsonValueKind.Object
                        && request.TryGetProperty("subtype", out var subtype) && subtype.ValueKind == JsonValueKind.String
                        && subtype.GetString() == "can_use_tool";
                    return id.GetString();
                }
            }
            catch (JsonException)
            {
                return null;
            }
        }

        private void ReadStderr(Process process)
        {
            try
            {
                while (process.StandardError.ReadLine() != null) Interlocked.Increment(ref _stderrLines);
            }
            catch (Exception e) when (e is IOException || e is ObjectDisposedException || e is InvalidOperationException)
            {
            }
        }

        /// <summary>The end comes from the process, not from the end of stdout.</summary>
        private async Task WatchAsync(Process process, Task exited)
        {
            int? code = null;
            try
            {
                await exited.ConfigureAwait(false);
                code = process.ExitCode;
            }
            catch (Exception e) when (e is InvalidOperationException || e is SystemException)
            {
            }
            // the last lines (the result) before the end is told; not for ever
            await Task.WhenAny(Task.WhenAll(_stdoutReader, _stderrReader), Task.Delay(DrainTimeout)).ConfigureAwait(false);
            Complete(code);
        }

        private void Complete(int? code)
        {
            bool first;
            lock (_gate)
            {
                first = !_completed;
                _completed = true;
                if (code != null && _exitCode == null) _exitCode = code;
                if (first) _stdin = null;
            }
            if (!first) return;
            _available.Release();
            _ended.TrySetResult(true);
            Raise(TransportState.Closed);
        }

        private static void KillTree(Process process)
        {
            try
            {
                if (process.HasExited) return;
#if NET6_0_OR_GREATER
                process.Kill(entireProcessTree: true);
#else
                if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
                {
                    // .NET Standard 2.0 has no tree kill: taskkill ends the process and its children. By its full
                    // path in the system folder, so no other taskkill on the PATH or in the current folder runs.
                    var taskkill = new ProcessStartInfo(
                        Path.Combine(Environment.SystemDirectory, "taskkill.exe"), "/PID " + process.Id + " /T /F")
                    {
                        UseShellExecute = false,
                        CreateNoWindow = true,
                        RedirectStandardOutput = true,
                        RedirectStandardError = true,
                    };
                    using (var killer = new Process { StartInfo = taskkill })
                    {
                        // what it says is read as it comes and dropped, so the wait below is the only one
                        killer.OutputDataReceived += (_, __) => { };
                        killer.ErrorDataReceived += (_, __) => { };
                        killer.Start();
                        killer.BeginOutputReadLine();
                        killer.BeginErrorReadLine();
                        killer.WaitForExit(15000);
                    }
                    if (!process.HasExited) process.Kill();
                }
                else
                {
                    process.Kill();
                }
#endif
            }
            catch (Exception e) when (e is InvalidOperationException || e is System.ComponentModel.Win32Exception
                || e is NotSupportedException || e is AggregateException)
            {
                // it ended on its own in the meantime, or a process of the tree could not be ended
            }
        }
    }
}
