using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Xunit;

namespace ClaudeCliProtocol.Tests
{
    /// <summary>The examples of libs/dotnet/README.md, so what the README shows compiles and runs.</summary>
    public class ReadmeExamplesTests
    {
        [Fact]
        public void Read_a_line_the_claude_wrote()
        {
            var line = FixtureFiles.StreamLines("edit")[0];
            var names = new List<string>();

            // --- README ---
            var parser = new StreamJsonParser(); // one per conversation: it keeps state between lines
            foreach (var e in parser.Parse(line))
            {
                if (e is SessionStarted started) names.Add("session " + started.SessionId);
            }
            // --- /README ---

            Assert.StartsWith("session ", Assert.Single(names));
        }

        [Fact]
        public async Task Play_a_recording_through_a_session()
        {
            var path = Path.Combine(FixtureFiles.Directory, "edit.stream.ndjson");
            var answered = 0;

            // --- README ---
            await using var session = new ClaudeSession(ReplayTransport.FromFile(path));
            await foreach (var e in session.ReadEventsAsync())
            {
                if (e is PermissionRequested request)
                {
                    // the answer names the request and the decision; the tool's input is not the caller's to send
                    await session.SendAsync(new AnswerPermission(request.RequestId, PermissionDecision.Allow));
                    answered++;
                }
            }
            // --- /README ---

            Assert.Equal(2, answered); // the recording asks about an Edit and a Write
            Assert.False(session.IsBusy);
        }

        [Fact]
        public async Task Talk_to_the_claude_of_this_machine()
        {
            // the make-believe claude plays a whole real recording: an Edit and a Write, each asked about
            var projectFolder = FakeClaudeHost.TempFolder();
            var options = new ProcessTransportOptions(projectFolder)
            {
                ClaudePath = FakeClaudeHost.Path,
                Environment = new Dictionary<string, string?>
                {
                    ["FAKE_CLAUDE_SCRIPT"] = Path.Combine(FixtureFiles.Directory, "edit.stream.ndjson"),
                },
            };
            var reply = new System.Text.StringBuilder();
            var asked = 0;
            var ended = false;
            var events = 0;
            var where = "before the start";
            var busyAtTheEnd = true;

            var talk = Task.Run(async () =>
            {
            // --- README ---
            var transport = new ProcessTransport(options); // new ProcessTransportOptions(projectFolder)
            await using var session = new ClaudeSession(transport);
            transport.Start();

            where = "started";
            await session.SendAsync(new SendUserMessage("Change line 3 of notes.md."));
            where = "message sent";
            await foreach (var e in session.ReadEventsAsync())
            {
                events++;
                where = "at " + e.GetType().Name;
                if (e is BlockDelta { Kind: DeltaKind.Text, ParentToolUseId: null } delta)
                {
                    reply.Append(delta.Text); // the reply, piece by piece
                }
                else if (e is PermissionRequested request)
                {
                    // the transport writes the answer with the input it read from the claude itself
                    await session.SendAsync(new AnswerPermission(request.RequestId, PermissionDecision.Allow));
                    asked++;
                    where = "after the answer " + asked;
                }
                else if (e is TurnCompleted)
                {
                    ended = true;
                    break;
                }
            }
            // --- /README ---
            busyAtTheEnd = session.IsBusy;
            where = "disposing";
            });
            if (await Task.WhenAny(talk, Task.Delay(TimeSpan.FromSeconds(90))) != talk)
                Assert.Fail("The conversation did not end: " + where + ", " + events + " events, " + asked + " answers.");
            await talk;

            Assert.True(ended);
            Assert.Equal(2, asked);
            Assert.True(reply.Length > 0);
            Assert.False(busyAtTheEnd);
        }

        [Fact]
        public void Write_a_line_for_the_claude()
        {
            var parser = new StreamJsonParser();
            var ledger = new PermissionLedger();
            foreach (var e in FixtureFiles.StreamLines("edit").SelectMany(parser.Parse))
            {
                ledger.Observe(e);
                if (ledger.Pending.Count > 0) break;
            }
            var answer = new AnswerPermission(ledger.Pending[0].RequestId, PermissionDecision.Deny, "not this file");
            var lines = new List<string>();

            // --- README ---
            var message = StdinWriter.Write(new SendUserMessage("hello"));

            // the ledger saw the request when it was read from the claude's stdout; it hands each request out once
            if (ledger.TryTake(answer.RequestId, out var request))
            {
                var line = StdinWriter.Write(answer, request!);
                lines.Add(line);
            }
            // --- /README ---

            Assert.StartsWith("{\"type\":\"user\"", message);
            Assert.Contains("\"behavior\":\"deny\"", Assert.Single(lines));
            Assert.False(ledger.TryTake(answer.RequestId, out _)); // the second answer finds nothing
        }
    }
}
