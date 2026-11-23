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
        public void Write_a_line_for_the_claude()
        {
            var parser = new StreamJsonParser();
            var request = FixtureFiles.StreamLines("edit").SelectMany(parser.Parse).OfType<PermissionRequested>().First();

            // --- README ---
            var message = StdinWriter.Write(new SendUserMessage("hello"));
            var answer = StdinWriter.Write(new AnswerPermission(request.RequestId, PermissionDecision.Deny, "not this file"), request);
            // --- /README ---

            Assert.StartsWith("{\"type\":\"user\"", message);
            Assert.Contains("\"behavior\":\"deny\"", answer);
        }
    }
}
