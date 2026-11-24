using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

namespace ClaudeCliProtocol.Tests
{
    /// <summary>The edges of a transport's life: a start that fails, a dispose in the middle, subscribers that throw.</summary>
    public class ProcessTransportLifetimeTests
    {
        private static ProcessTransportOptions Options(params (string Name, string? Value)[] environment) =>
            ProcessTransportTests.Options(environment);

        [Fact]
        public void A_dispose_during_the_start_leaves_no_claude_behind()
        {
            // the conversation is closed while it opens: the dispose finds no process yet, and the start goes on
            var transport = new ProcessTransport(Options());
            transport.BeforeSpawn = () => transport.DisposeAsync().AsTask().GetAwaiter().GetResult();

            Assert.Throws<ObjectDisposedException>(() => transport.Start());

            var pid = transport.SpawnedProcessId;
            Assert.NotNull(pid);
            Assert.True(SpinWait.SpinUntil(() => ProcessTransportTests.IsGone(pid!.Value), ProcessTransportTests.Patience),
                "The claude started during the dispose is still running.");
        }

        [Fact]
        public async Task A_subscriber_that_throws_as_the_transport_opens_does_not_stop_the_start()
        {
            await using var transport = new ProcessTransport(Options());
            transport.StateChanged += (_, state) =>
            {
                if (state == TransportState.Open) throw new InvalidOperationException("a subscriber's own trouble");
            };

            transport.Start();

            Assert.Equal("fake_start", (await ProcessTransportTests.Next(transport)).GetProperty("type").GetString());
        }

        [Fact]
        public async Task A_subscriber_that_throws_as_the_transport_closes_does_not_silence_the_others()
        {
            var heard = new List<TransportState>();
            await using var transport = new ProcessTransport(Options(("FAKE_CLAUDE_EXIT_NOW", "0")));
            transport.StateChanged += (_, state) =>
            {
                if (state == TransportState.Closed) throw new InvalidOperationException("a subscriber's own trouble");
            };
            transport.StateChanged += (_, state) => { lock (heard) heard.Add(state); };

            transport.Start();
            while (await ProcessTransportTests.Soon(transport.ReceiveLineAsync(CancellationToken.None)) != null)
            {
            }

            Assert.True(SpinWait.SpinUntil(() => { lock (heard) return heard.Contains(TransportState.Closed); }, TimeSpan.FromSeconds(20)),
                "The second subscriber never heard that the transport closed.");
        }

        [Fact]
        public async Task A_subscriber_that_throws_does_not_break_the_dispose_of_a_transport_never_started()
        {
            var transport = new ProcessTransport(Options());
            transport.StateChanged += (_, __) => throw new InvalidOperationException("a subscriber's own trouble");

            await transport.DisposeAsync();
        }

        [Fact]
        public async Task A_start_that_fails_leaves_the_transport_closed()
        {
            var folder = FakeClaudeHost.TempFolder();
            await using var transport = new ProcessTransport(
                new ProcessTransportOptions(folder) { ClaudePath = Path.Combine(folder, "no-claude-here.exe") });
            Assert.Throws<FileNotFoundException>(() => transport.Start());

            var read = transport.ReceiveLineAsync(CancellationToken.None);

            Assert.True(await Task.WhenAny(read, Task.Delay(TimeSpan.FromSeconds(5))) == read, "The read waits for a claude that never started.");
            Assert.Null(await read);
        }

        [Fact]
        public void A_claude_path_that_is_not_absolute_is_refused()
        {
            // a bare name would be looked for in the current folder, which may be the conversation's
            var folder = FakeClaudeHost.TempFolder();

            Assert.Throws<ArgumentException>(() => new ProcessTransport(new ProcessTransportOptions(folder) { ClaudePath = "claude" }));
            Assert.Throws<ArgumentException>(() =>
                new ProcessTransport(new ProcessTransportOptions(folder) { ClaudePath = Path.Combine("tools", "claude.exe") }));
        }
    }
}
