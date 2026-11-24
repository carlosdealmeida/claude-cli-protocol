using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

namespace ClaudeCliProtocol.Tests
{
    public class ReplayTransportTests
    {
        private static async Task<List<string>> ReadAll(IClaudeTransport transport)
        {
            var lines = new List<string>();
            while (await transport.ReceiveLineAsync(CancellationToken.None) is { } line) lines.Add(line);
            return lines;
        }

        [Fact]
        public async Task It_gives_the_lines_in_order_and_then_null()
        {
            await using var transport = new ReplayTransport(new[] { "{\"a\":1}", "{\"b\":2}", "{\"c\":3}" });

            Assert.Equal("{\"a\":1}", await transport.ReceiveLineAsync(CancellationToken.None));
            Assert.Equal("{\"b\":2}", await transport.ReceiveLineAsync(CancellationToken.None));
            Assert.Equal("{\"c\":3}", await transport.ReceiveLineAsync(CancellationToken.None));
            Assert.Null(await transport.ReceiveLineAsync(CancellationToken.None));
        }

        [Fact]
        public async Task Blank_lines_are_skipped_and_a_trailing_carriage_return_is_removed()
        {
            await using var transport = new ReplayTransport(new[] { "", "{\"a\":1}\r", "   ", "\r", "{\"b\":2}" });

            Assert.Equal(new[] { "{\"a\":1}", "{\"b\":2}" }, await ReadAll(transport));
        }

        [Fact]
        public async Task After_the_end_it_keeps_giving_null()
        {
            await using var transport = new ReplayTransport(new[] { "{}" });
            await ReadAll(transport);

            Assert.Null(await transport.ReceiveLineAsync(CancellationToken.None));
            Assert.Null(await transport.ReceiveLineAsync(CancellationToken.None));
        }

        [Fact]
        public async Task Commands_are_ignored()
        {
            await using var transport = new ReplayTransport(new[] { "{\"a\":1}", "{\"b\":2}" });

            await transport.SendAsync(new SendUserMessage("hello"), CancellationToken.None);
            await transport.SendAsync(new AnswerPermission("r1", PermissionDecision.Allow), CancellationToken.None);
            await transport.SendAsync(new Interrupt(), CancellationToken.None);

            Assert.Equal(new[] { "{\"a\":1}", "{\"b\":2}" }, await ReadAll(transport));
            await transport.SendAsync(new SendUserMessage("after the end"), CancellationToken.None);
        }

        [Fact]
        public async Task It_reports_closed_once_at_the_end()
        {
            var states = new List<TransportState>();
            var transport = new ReplayTransport(new[] { "{\"a\":1}" });
            transport.StateChanged += (_, state) => states.Add(state);

            Assert.NotNull(await transport.ReceiveLineAsync(CancellationToken.None));
            Assert.Empty(states); // nothing to report while there are lines

            Assert.Null(await transport.ReceiveLineAsync(CancellationToken.None));
            Assert.Equal(new[] { TransportState.Closed }, states);

            Assert.Null(await transport.ReceiveLineAsync(CancellationToken.None));
            await transport.DisposeAsync();
            Assert.Equal(new[] { TransportState.Closed }, states);
        }

        [Fact]
        public async Task A_cancelled_token_stops_the_read()
        {
            await using var transport = new ReplayTransport(new[] { "{\"a\":1}" });
            using var cancelled = new CancellationTokenSource();
            cancelled.Cancel();

            await Assert.ThrowsAnyAsync<OperationCanceledException>(() => transport.ReceiveLineAsync(cancelled.Token));
            // the line was not consumed
            Assert.Equal("{\"a\":1}", await transport.ReceiveLineAsync(CancellationToken.None));
        }

        [Fact]
        public async Task After_dispose_it_gives_null()
        {
            var states = new List<TransportState>();
            var transport = new ReplayTransport(new[] { "{\"a\":1}", "{\"b\":2}" });
            transport.StateChanged += (_, state) => states.Add(state);
            Assert.NotNull(await transport.ReceiveLineAsync(CancellationToken.None));

            await transport.DisposeAsync();

            Assert.Null(await transport.ReceiveLineAsync(CancellationToken.None));
            Assert.Equal(new[] { TransportState.Closed }, states);
            await transport.DisposeAsync(); // a second dispose does nothing
            Assert.Equal(new[] { TransportState.Closed }, states);
        }

        [Fact]
        public async Task It_reads_a_recording_from_a_file()
        {
            await using var transport = ReplayTransport.FromFile(Path.Combine(FixtureFiles.Directory, "edit.stream.ndjson"));

            Assert.Equal(FixtureFiles.StreamLines("edit"), (await ReadAll(transport)).ToArray());
        }

        [Fact]
        public void Null_lines_are_rejected()
        {
            Assert.Throws<ArgumentNullException>(() => new ReplayTransport(null!));
        }
    }
}
