using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

namespace ClaudeCliProtocol.Tests
{
    /// <summary>The transport over the make-believe claude: starting, reading and ending. No real claude runs here.</summary>
    public class ProcessTransportTests
    {
        internal static readonly TimeSpan Patience = TimeSpan.FromSeconds(60);

        /// <summary>Options for the make-believe claude, in a new empty folder, with no initialize.</summary>
        internal static ProcessTransportOptions Options(params (string Name, string? Value)[] environment) =>
            new ProcessTransportOptions(FakeClaudeHost.TempFolder())
            {
                ClaudePath = FakeClaudeHost.Path,
                SendInitialize = false,
                Environment = environment.ToDictionary(e => e.Name, e => e.Value),
            };

        internal static async Task<T> Soon<T>(Task<T> task)
        {
            if (await Task.WhenAny(task, Task.Delay(Patience)) != task) throw new TimeoutException("The transport did not answer in time.");
            return await task;
        }

        internal static async Task<JsonElement> Next(ProcessTransport transport)
        {
            var line = await Soon(transport.ReceiveLineAsync(CancellationToken.None));
            Assert.NotNull(line);
            using var doc = JsonDocument.Parse(line!);
            return doc.RootElement.Clone();
        }

        private static async Task<List<string>> ReadToTheEnd(ProcessTransport transport)
        {
            var lines = new List<string>();
            while (await Soon(transport.ReceiveLineAsync(CancellationToken.None)) is { } line) lines.Add(line);
            return lines;
        }

        internal static bool IsGone(int pid)
        {
            try
            {
                using var process = Process.GetProcessById(pid);
                return process.HasExited;
            }
            catch (ArgumentException)
            {
                return true;
            }
            catch (InvalidOperationException)
            {
                return true;
            }
        }

        [Fact]
        public async Task It_starts_the_claude_with_the_checked_arguments_in_the_given_folder()
        {
            var options = Options() with { PermissionMode = "plan", Model = "opus[1m]", Effort = "high" };
            await using var transport = new ProcessTransport(options);
            transport.Start();

            var start = await Next(transport);

            Assert.Equal("fake_start", start.GetProperty("type").GetString());
            Assert.Equal(ClaudeArguments.Build(options).ToArray(),
                start.GetProperty("args").EnumerateArray().Select(a => a.GetString()).ToArray());
            Assert.Equal(Path.GetFullPath(options.WorkingDirectory).TrimEnd(Path.DirectorySeparatorChar),
                Path.GetFullPath(start.GetProperty("cwd").GetString()!).TrimEnd(Path.DirectorySeparatorChar), ignoreCase: true);
            Assert.Equal("1", start.GetProperty("env").GetProperty("NoDefaultCurrentDirectoryInExePath").GetString());
        }

        [Fact]
        public async Task The_environment_of_the_options_is_added_and_a_null_value_removes()
        {
            Environment.SetEnvironmentVariable("FAKE_FROM_THIS_MACHINE", "kept");
            Environment.SetEnvironmentVariable("FAKE_TO_REMOVE", "there");

            await using var transport = new ProcessTransport(Options(
                ("FAKE_ADDED", "1"), ("CLAUDE_CONFIG_DIR", "another-account"), ("FAKE_TO_REMOVE", null)));
            transport.Start();
            var env = (await Next(transport)).GetProperty("env");

            Assert.Equal("1", env.GetProperty("FAKE_ADDED").GetString());
            Assert.Equal("another-account", env.GetProperty("CLAUDE_CONFIG_DIR").GetString());
            Assert.Equal("kept", env.GetProperty("FAKE_FROM_THIS_MACHINE").GetString()); // this machine's environment goes along
            Assert.False(env.TryGetProperty("FAKE_TO_REMOVE", out _));
        }

        [Fact]
        public async Task It_gives_the_lines_of_stdout_in_order_and_then_null()
        {
            var script = FakeClaudeHost.Script("{\"n\":1}", "", "{\"n\":2}", "{\"n\":3}");
            await using var transport = new ProcessTransport(Options(("FAKE_CLAUDE_SCRIPT", script), ("FAKE_CLAUDE_EXIT_NOW", "0")));
            transport.Start();

            var lines = await ReadToTheEnd(transport);

            Assert.Equal(4, lines.Count); // the start line and the three of the script; the blank one is skipped
            Assert.Equal(new[] { "{\"n\":1}", "{\"n\":2}", "{\"n\":3}" }, lines.Skip(1).ToArray());
            Assert.Null(await Soon(transport.ReceiveLineAsync(CancellationToken.None))); // and null again
            Assert.Equal(0, transport.ExitCode);
        }

        [Fact]
        public async Task A_process_that_exits_at_once_closes_the_transport_with_its_exit_code()
        {
            var states = new List<TransportState>();
            await using var transport = new ProcessTransport(Options(("FAKE_CLAUDE_EXIT_NOW", "3")));
            transport.StateChanged += (_, state) => { lock (states) states.Add(state); };
            Assert.Null(transport.ExitCode);
            transport.Start();

            var lines = await ReadToTheEnd(transport);

            Assert.Single(lines); // the start line, then the end
            Assert.Equal(3, transport.ExitCode);
            lock (states) Assert.Equal(new[] { TransportState.Open, TransportState.Closed }, states.ToArray());
        }

        [Fact]
        public async Task A_flood_on_stderr_does_not_block_the_process()
        {
            var script = FakeClaudeHost.Script(Enumerable.Range(1, 50).Select(n => "{\"n\":" + n + "}").ToArray());
            await using var transport = new ProcessTransport(Options(
                ("FAKE_CLAUDE_STDERR_LINES", "20000"), ("FAKE_CLAUDE_SCRIPT", script), ("FAKE_CLAUDE_EXIT_NOW", "0")));
            transport.Start();

            var lines = await ReadToTheEnd(transport);

            Assert.Equal(51, lines.Count);
            Assert.Equal(20000, transport.StderrLines); // counted, and nothing of them kept
        }

        [Fact]
        public async Task Lines_are_read_as_utf8()
        {
            var script = FakeClaudeHost.Script("{\"text\":\"ação 日本 “aspas”\"}");
            await using var transport = new ProcessTransport(Options(("FAKE_CLAUDE_SCRIPT", script), ("FAKE_CLAUDE_EXIT_NOW", "0")));
            transport.Start();
            await Next(transport);

            Assert.Equal("ação 日本 “aspas”", (await Next(transport)).GetProperty("text").GetString());
        }

        [Fact]
        public async Task Dispose_ends_the_process_and_its_children()
        {
            var transport = new ProcessTransport(Options(("FAKE_CLAUDE_CHILD", "1")));
            transport.Start();
            await Next(transport);
            var child = (await Next(transport)).GetProperty("pid").GetInt32();
            Assert.False(IsGone(child));

            await transport.DisposeAsync();

            Assert.NotNull(transport.ExitCode);
            Assert.True(SpinWait.SpinUntil(() => IsGone(child), TimeSpan.FromSeconds(20)), "the child is still running");
            Assert.Null(await Soon(transport.ReceiveLineAsync(CancellationToken.None)));
            await transport.DisposeAsync(); // a second dispose does nothing
        }

        [Fact]
        public async Task The_end_comes_from_the_process_not_from_the_end_of_stdout()
        {
            // the claude exits and a child of it still holds the stdout it inherited: the transport must not wait for it
            var transport = new ProcessTransport(Options(("FAKE_CLAUDE_CHILD", "1"))) { DrainTimeout = TimeSpan.FromMilliseconds(500) };
            transport.Start();
            await Next(transport);
            var child = (await Next(transport)).GetProperty("pid").GetInt32();
            try
            {
                await Soon(transport.SendRawAsync("{\"type\":\"fake_exit\",\"code\":5}"));

                Assert.Null(await Soon(transport.ReceiveLineAsync(CancellationToken.None)));
                Assert.Equal(5, transport.ExitCode);
            }
            finally
            {
                await transport.DisposeAsync();
                try
                {
                    using var orphan = Process.GetProcessById(child);
                    orphan.Kill(); // the claude had already ended by itself: its orphan is this test's to end
                }
                catch (ArgumentException)
                {
                }
                catch (InvalidOperationException)
                {
                }
            }
        }

        [Fact]
        public async Task A_cancelled_read_stops_and_the_next_read_still_works()
        {
            await using var transport = new ProcessTransport(Options());
            transport.Start();
            using var cancelled = new CancellationTokenSource();
            cancelled.Cancel();

            await Assert.ThrowsAnyAsync<OperationCanceledException>(() => transport.ReceiveLineAsync(cancelled.Token));

            Assert.Equal("fake_start", (await Next(transport)).GetProperty("type").GetString()); // the line was not lost

            // and a read that is cancelled while it waits
            using var later = new CancellationTokenSource(TimeSpan.FromMilliseconds(200));
            await Assert.ThrowsAnyAsync<OperationCanceledException>(() => Soon(transport.ReceiveLineAsync(later.Token)));
        }

        [Fact]
        public void A_missing_claude_or_folder_or_a_refused_value_says_so()
        {
            var folder = FakeClaudeHost.TempFolder();

            Assert.Throws<FileNotFoundException>(() =>
                new ProcessTransport(new ProcessTransportOptions(folder) { ClaudePath = Path.Combine(folder, "no-claude-here.exe") }).Start());
            Assert.Throws<DirectoryNotFoundException>(() =>
                new ProcessTransport(new ProcessTransportOptions(Path.Combine(folder, "missing")) { ClaudePath = FakeClaudeHost.Path }).Start());
            // a refused value never gets as far as a transport
            Assert.Throws<ArgumentException>(() =>
                new ProcessTransport(new ProcessTransportOptions(folder) { ClaudePath = FakeClaudeHost.Path, Model = "sonnet & calc" }));
            Assert.Throws<ArgumentNullException>(() => new ProcessTransport(null!));
        }

        [Fact]
        public async Task It_must_be_started_once_before_it_is_read()
        {
            await using var transport = new ProcessTransport(Options(("FAKE_CLAUDE_EXIT_NOW", "0")));

            await Assert.ThrowsAsync<InvalidOperationException>(() => transport.ReceiveLineAsync(CancellationToken.None));
            transport.Start();
            Assert.Throws<InvalidOperationException>(() => transport.Start());
        }

        [Fact]
        public async Task A_transport_never_started_is_disposed_quietly()
        {
            var transport = new ProcessTransport(Options());

            await transport.DisposeAsync();

            Assert.Null(transport.ExitCode);
            Assert.Throws<ObjectDisposedException>(() => transport.Start());
        }
    }
}
