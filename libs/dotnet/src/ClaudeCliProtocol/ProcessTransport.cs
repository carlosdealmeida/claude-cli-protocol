using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace ClaudeCliProtocol
{
    /// <summary>
    /// The transport that runs the claude on this machine: it starts <c>claude -p</c> in stream-json with checked
    /// arguments (<see cref="ClaudeArguments"/>), gives the lines of its stdout and writes the commands to its stdin.
    /// The stderr is read and dropped, because it may hold text of the conversation; reading it is needed, or the
    /// pipe fills and the claude stops. Nothing is written to disk.
    /// </summary>
    public sealed class ProcessTransport : IClaudeTransport
    {
        private readonly ProcessTransportOptions _options;
        private readonly IReadOnlyList<string> _arguments;
        private readonly object _gate = new object();
        private readonly ConcurrentQueue<string> _lines = new ConcurrentQueue<string>();
        private readonly SemaphoreSlim _available = new SemaphoreSlim(0);
        private readonly SemaphoreSlim _writing = new SemaphoreSlim(1, 1);
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

        /// <summary>Checks the options at once: a refused value throws <see cref="ArgumentException"/> here.</summary>
        public ProcessTransport(ProcessTransportOptions options)
        {
            _options = options ?? throw new ArgumentNullException(nameof(options));
            _arguments = ClaudeArguments.Build(options);
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

        public event EventHandler<TransportState>? StateChanged;

        /// <summary>
        /// Starts the claude. <see cref="FileNotFoundException"/> when there is no claude;
        /// <see cref="DirectoryNotFoundException"/> when the working directory does not exist. Call it once.
        /// </summary>
        public void Start()
        {
            lock (_gate)
            {
                if (_disposed) throw new ObjectDisposedException(nameof(ProcessTransport));
                if (_started) throw new InvalidOperationException("The transport was already started.");
                _started = true;
            }

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
            process.Start();
            lock (_gate)
            {
                _process = process;
                _stdin = process.StandardInput.BaseStream;
            }
            StateChanged?.Invoke(this, TransportState.Open);

            _stdoutReader = Task.Run(() => ReadStdout(process));
            _stderrReader = Task.Run(() => ReadStderr(process));
            _ = Task.Run(() => WatchAsync(process));
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

        /// <summary>Written in the next task.</summary>
        public Task SendAsync(ClaudeCommand command, CancellationToken ct) => Task.CompletedTask;

        /// <summary>One line to the claude's stdin, as ASCII bytes and a line feed. False when the process has ended.</summary>
        internal async Task<bool> SendRawAsync(string line)
        {
            var bytes = Encoding.ASCII.GetBytes(line + "\n");
            await _writing.WaitAsync().ConfigureAwait(false);
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

        private void ReadStdout(Process process)
        {
            try
            {
                string? line;
                while ((line = process.StandardOutput.ReadLine()) != null)
                {
                    line = line.TrimEnd('\r');
                    if (line.Trim().Length == 0) continue;
                    _lines.Enqueue(line);
                    _available.Release();
                }
            }
            catch (Exception e) when (e is IOException || e is ObjectDisposedException || e is InvalidOperationException)
            {
                // the pipe went away with the process
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
        private async Task WatchAsync(Process process)
        {
            int? code = null;
            try
            {
                await Task.Run(() => process.WaitForExit(Timeout.Infinite)).ConfigureAwait(false);
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
            StateChanged?.Invoke(this, TransportState.Closed);
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
                    using (var killer = Process.Start(taskkill))
                    {
                        killer?.StandardOutput.ReadToEnd();
                        killer?.WaitForExit(15000);
                    }
                    if (!process.HasExited) process.Kill();
                }
                else
                {
                    process.Kill();
                }
#endif
            }
            catch (Exception e) when (e is InvalidOperationException || e is System.ComponentModel.Win32Exception || e is NotSupportedException)
            {
                // it ended on its own in the meantime
            }
        }
    }
}
