using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

namespace ClaudeCliProtocol.Tests
{
    /// <summary>
    /// What the transport writes to the claude's stdin. The make-believe claude sends every line it reads back as
    /// {"type":"fake_stdin","line":"..."}, and that is how these tests see what was written.
    /// </summary>
    public class ProcessTransportSendTests
    {
        private static readonly string CanUseTool = FixtureFiles.StreamLines("edit").First(l => l.Contains("\"can_use_tool\""));

        private static ProcessTransportOptions Options(params (string Name, string? Value)[] environment) =>
            ProcessTransportTests.Options(environment);

        private static Task<JsonElement> Next(ProcessTransport transport) => ProcessTransportTests.Next(transport);

        private static JsonElement J(string json)
        {
            using var doc = JsonDocument.Parse(json);
            return doc.RootElement.Clone();
        }

        private static string RequestId(string canUseToolLine) => J(canUseToolLine).GetProperty("request_id").GetString()!;

        /// <summary>The next line the claude read on its stdin, as the text that was written.</summary>
        private static async Task<string> Written(ProcessTransport transport)
        {
            var echo = await Next(transport);
            Assert.Equal("fake_stdin", echo.GetProperty("type").GetString());
            return echo.GetProperty("line").GetString()!;
        }

        private static async Task<ProcessTransport> Started(params (string Name, string? Value)[] environment)
        {
            var transport = new ProcessTransport(Options(environment));
            transport.Start();
            Assert.Equal("fake_start", (await Next(transport)).GetProperty("type").GetString());
            return transport;
        }

        /// <summary>Sends a message and reads it back: whatever was written before it has already come.</summary>
        private static async Task AssertNothingWasWrittenBefore(ProcessTransport transport, string marker)
        {
            await transport.SendAsync(new SendUserMessage(marker), CancellationToken.None);
            Assert.Equal(marker, J(await Written(transport)).GetProperty("message").GetProperty("content").GetString());
        }

        [Fact]
        public async Task Initialize_goes_first()
        {
            await using var transport = new ProcessTransport(Options() with { SendInitialize = true });
            transport.Start();
            await transport.SendAsync(new SendUserMessage("hello"), CancellationToken.None);
            Assert.Equal("fake_start", (await Next(transport)).GetProperty("type").GetString());

            var first = J(await Written(transport));
            var second = J(await Written(transport));

            Assert.Equal("control_request", first.GetProperty("type").GetString());
            Assert.Equal("initialize", first.GetProperty("request").GetProperty("subtype").GetString());
            Assert.Matches("^[0-9a-f]{32}$", first.GetProperty("request_id").GetString());
            Assert.Equal("user", second.GetProperty("type").GetString());
        }

        [Fact]
        public async Task Start_does_not_wait_for_a_claude_that_talks_before_it_listens()
        {
            // a claude that writes a lot as soon as it starts, before it reads its stdin: Start must come back,
            // and the initialize must still be the first line written
            var script = FakeClaudeHost.Script(Enumerable.Range(0, 400).Select(n => "{\"n\":" + n + ",\"pad\":\"" + new string('x', 200) + "\"}").ToArray());
            var transport = new ProcessTransport(Options(("FAKE_CLAUDE_SCRIPT", script)) with { SendInitialize = true });
            try
            {
                var start = Task.Run(() => transport.Start());
                Assert.True(await Task.WhenAny(start, Task.Delay(TimeSpan.FromSeconds(30))) == start, "Start did not come back.");
                await start;
                await transport.SendAsync(new SendUserMessage("hello"), CancellationToken.None);

                Assert.Equal("fake_start", (await Next(transport)).GetProperty("type").GetString());
                for (var i = 0; i < 400; i++) Assert.Equal(i, (await Next(transport)).GetProperty("n").GetInt32());
                Assert.Equal("initialize", J(await Written(transport)).GetProperty("request").GetProperty("subtype").GetString());
                Assert.Equal("user", J(await Written(transport)).GetProperty("type").GetString());
            }
            finally
            {
                await transport.DisposeAsync();
            }
        }

        [Fact]
        public async Task A_user_message_is_written_as_the_writer_writes_it()
        {
            await using var transport = await Started();
            var message = new SendUserMessage("hello") { Uuid = "8c1f6a52-0000-4000-8000-000000000001" };

            await transport.SendAsync(message, CancellationToken.None);

            Assert.Equal(StdinWriter.Write(message), await Written(transport));
        }

        [Fact]
        public async Task A_control_command_keeps_its_request_id_and_gets_one_when_it_has_none()
        {
            await using var transport = await Started();

            await transport.SendAsync(new SetPermissionMode("plan") { RequestId = "mine" }, CancellationToken.None);
            await transport.SendAsync(new Interrupt(), CancellationToken.None);

            Assert.Equal(StdinWriter.Write(new SetPermissionMode("plan") { RequestId = "mine" }), await Written(transport));
            var interrupt = J(await Written(transport));
            Assert.Equal("interrupt", interrupt.GetProperty("request").GetProperty("subtype").GetString());
            Assert.Matches("^[0-9a-f]{32}$", interrupt.GetProperty("request_id").GetString());
        }

        [Fact]
        public async Task The_answer_carries_the_input_this_transport_read()
        {
            await using var transport = await Started(("FAKE_CLAUDE_SCRIPT", FakeClaudeHost.Script(CanUseTool)));
            var asked = await Next(transport); // the request, as the claude wrote it
            var id = asked.GetProperty("request_id").GetString()!;

            await transport.SendAsync(new AnswerPermission(id, PermissionDecision.AllowAlways), CancellationToken.None);

            var response = J(await Written(transport)).GetProperty("response");
            Assert.Equal(id, response.GetProperty("request_id").GetString());
            var body = response.GetProperty("response");
            Assert.Equal("allow", body.GetProperty("behavior").GetString());
            Assert.Equal(JsonSerializer.Serialize(asked.GetProperty("request").GetProperty("input")),
                JsonSerializer.Serialize(body.GetProperty("updatedInput")));
            Assert.Equal(JsonSerializer.Serialize(asked.GetProperty("request").GetProperty("permission_suggestions")),
                JsonSerializer.Serialize(body.GetProperty("updatedPermissions")));
        }

        [Fact]
        public async Task A_second_answer_writes_nothing()
        {
            await using var transport = await Started(("FAKE_CLAUDE_SCRIPT", FakeClaudeHost.Script(CanUseTool)));
            var id = (await Next(transport)).GetProperty("request_id").GetString()!;
            await transport.SendAsync(new AnswerPermission(id, PermissionDecision.Deny, "no"), CancellationToken.None);
            Assert.Equal("deny", J(await Written(transport)).GetProperty("response").GetProperty("response").GetProperty("behavior").GetString());

            await transport.SendAsync(new AnswerPermission(id, PermissionDecision.Allow), CancellationToken.None);

            await AssertNothingWasWrittenBefore(transport, "marker");
        }

        [Fact]
        public async Task An_answer_to_a_cancelled_request_writes_nothing()
        {
            var id = RequestId(CanUseTool);
            var cancel = "{\"type\":\"control_cancel_request\",\"request_id\":\"" + id + "\"}";
            await using var transport = await Started(("FAKE_CLAUDE_SCRIPT", FakeClaudeHost.Script(CanUseTool, cancel)));
            await Next(transport);
            await Next(transport); // the cancel was read

            await transport.SendAsync(new AnswerPermission(id, PermissionDecision.Allow), CancellationToken.None);

            await AssertNothingWasWrittenBefore(transport, "marker");
        }

        [Fact]
        public async Task An_answer_to_a_request_never_seen_writes_nothing()
        {
            await using var transport = await Started();

            await transport.SendAsync(new AnswerPermission("never-asked", PermissionDecision.Allow), CancellationToken.None);

            await AssertNothingWasWrittenBefore(transport, "marker");
        }

        [Fact]
        public async Task A_control_request_nobody_serves_is_answered_with_an_error()
        {
            var hook = "{\"type\":\"control_request\",\"request_id\":\"hook_1\",\"request\":{\"subtype\":\"hook_callback\",\"callback_id\":\"x\"}}";
            await using var transport = await Started(("FAKE_CLAUDE_SCRIPT", FakeClaudeHost.Script(hook, CanUseTool)));

            Assert.Equal("hook_1", (await Next(transport)).GetProperty("request_id").GetString()); // the line still comes out
            Assert.Equal("can_use_tool", (await Next(transport)).GetProperty("request").GetProperty("subtype").GetString());

            // without an answer the claude would wait for ever; the permission request is not answered by the transport
            Assert.Equal(
                "{\"type\":\"control_response\",\"response\":{\"subtype\":\"error\",\"request_id\":\"hook_1\",\"error\":\"not supported\"}}",
                await Written(transport));
            await AssertNothingWasWrittenBefore(transport, "marker");
        }

        [Fact]
        public async Task Every_line_written_is_ascii_and_comes_back_equal()
        {
            await using var transport = await Started();

            await transport.SendAsync(new SendUserMessage("ação “aspas” 日本\nsegunda linha"), CancellationToken.None);

            var written = await Written(transport);
            Assert.All(written, c => Assert.True(c < 128));
            Assert.Equal("ação “aspas” 日本\nsegunda linha", J(written).GetProperty("message").GetProperty("content").GetString());
        }

        [Fact]
        public async Task Sending_after_the_process_ended_does_nothing()
        {
            await using var transport = new ProcessTransport(Options(("FAKE_CLAUDE_EXIT_NOW", "0")));
            transport.Start();
            while (await ProcessTransportTests.Soon(transport.ReceiveLineAsync(CancellationToken.None)) != null)
            {
            }

            await transport.SendAsync(new SendUserMessage("too late"), CancellationToken.None);
            await transport.SendAsync(new Interrupt(), CancellationToken.None);
            await transport.SendAsync(new AnswerPermission("x", PermissionDecision.Allow), CancellationToken.None);

            Assert.Equal(0, transport.ExitCode);
        }

        [Fact]
        public async Task Sending_before_the_start_or_a_null_command_says_so()
        {
            await using var transport = new ProcessTransport(Options());

            await Assert.ThrowsAsync<InvalidOperationException>(() => transport.SendAsync(new Interrupt(), CancellationToken.None));
            await Assert.ThrowsAsync<ArgumentNullException>(() => transport.SendAsync(null!, CancellationToken.None));
        }

        [Fact]
        public async Task Writes_from_two_threads_do_not_mix()
        {
            await using var transport = await Started();

            await Task.WhenAll(
                Task.Run(async () => { for (var i = 0; i < 100; i++) await transport.SendAsync(new SendUserMessage("a-" + i), CancellationToken.None); }),
                Task.Run(async () => { for (var i = 0; i < 100; i++) await transport.SendAsync(new SendUserMessage("b-" + i), CancellationToken.None); }));

            var texts = new List<string>();
            for (var i = 0; i < 200; i++)
                texts.Add(J(await Written(transport)).GetProperty("message").GetProperty("content").GetString()!); // every line is whole JSON
            Assert.Equal(Enumerable.Range(0, 100).Select(i => "a-" + i).ToArray(), texts.Where(t => t.StartsWith("a-")).ToArray());
            Assert.Equal(Enumerable.Range(0, 100).Select(i => "b-" + i).ToArray(), texts.Where(t => t.StartsWith("b-")).ToArray());
        }

        [Fact]
        public async Task A_session_over_the_transport_answers_a_permission_request()
        {
            var transport = new ProcessTransport(Options(("FAKE_CLAUDE_SCRIPT", FakeClaudeHost.Script(CanUseTool))));
            var tap = new Tap(transport);
            await using var session = new ClaudeSession(tap);
            transport.Start();

            var reading = Task.Run(async () =>
            {
                await foreach (var e in session.ReadEventsAsync())
                {
                    if (!(e is PermissionRequested request)) continue;
                    Assert.Single(session.PendingPermissions);
                    Assert.True(await session.SendAsync(new AnswerPermission(request.RequestId, PermissionDecision.Allow)));
                    Assert.Empty(session.PendingPermissions);
                }
            });

            var written = J(await ProcessTransportTests.Soon(tap.FirstWritten.Task)); // what reached the claude's stdin
            await session.DisposeAsync(); // ends the claude; the reading ends
            await ProcessTransportTests.Soon(reading.ContinueWith(_ => true));
            await reading;

            var asked = J(CanUseTool);
            Assert.Equal(asked.GetProperty("request_id").GetString(), written.GetProperty("response").GetProperty("request_id").GetString());
            Assert.Equal(JsonSerializer.Serialize(asked.GetProperty("request").GetProperty("input")),
                JsonSerializer.Serialize(written.GetProperty("response").GetProperty("response").GetProperty("updatedInput")));
        }

        /// <summary>Lets every line through and keeps the first one the make-believe claude read on its stdin.</summary>
        private sealed class Tap : IClaudeTransport
        {
            private readonly IClaudeTransport _inner;

            public Tap(IClaudeTransport inner) => _inner = inner;

            public TaskCompletionSource<string> FirstWritten { get; } =
                new TaskCompletionSource<string>(TaskCreationOptions.RunContinuationsAsynchronously);

            public event EventHandler<TransportState>? StateChanged
            {
                add => _inner.StateChanged += value;
                remove => _inner.StateChanged -= value;
            }

            public async Task<string?> ReceiveLineAsync(CancellationToken ct)
            {
                var line = await _inner.ReceiveLineAsync(ct).ConfigureAwait(false);
                if (line != null && line.Contains("\"fake_stdin\""))
                {
                    using var doc = JsonDocument.Parse(line);
                    FirstWritten.TrySetResult(doc.RootElement.GetProperty("line").GetString()!);
                }
                return line;
            }

            public Task SendAsync(ClaudeCommand command, CancellationToken ct) => _inner.SendAsync(command, ct);

            public ValueTask DisposeAsync() => _inner.DisposeAsync();
        }
    }
}
