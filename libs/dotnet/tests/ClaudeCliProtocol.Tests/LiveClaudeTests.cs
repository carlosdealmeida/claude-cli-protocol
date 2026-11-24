using System;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

namespace ClaudeCliProtocol.Tests
{
    /// <summary>
    /// The one test that starts the real claude. It uses the claude of this machine, with whatever account it is
    /// signed in to, so it runs only when asked: set CCP_LIVE_CLAUDE=1. Without it the test returns at once.
    /// </summary>
    public class LiveClaudeTests
    {
        internal static bool Enabled => Environment.GetEnvironmentVariable("CCP_LIVE_CLAUDE") == "1";

        [Fact]
        public async Task A_real_claude_answers_one_message()
        {
            if (!Enabled) return;

            // plan mode, in a new empty folder: the claude may read, and writes nothing
            var transport = new ProcessTransport(new ProcessTransportOptions(FakeClaudeHost.TempFolder()) { PermissionMode = "plan" });
            await using var session = new ClaudeSession(transport);
            transport.Start();
            using var patience = new CancellationTokenSource(TimeSpan.FromSeconds(120));

            Assert.True(await session.SendAsync(new SendUserMessage("Reply with the single word: ready"), patience.Token));

            SessionStarted? started = null;
            TurnCompleted? completed = null;
            var text = new StringBuilder();
            await foreach (var e in session.ReadEventsAsync(patience.Token))
            {
                switch (e)
                {
                    case SessionStarted s:
                        started ??= s;
                        break;
                    case AssistantBlock { ParentToolUseId: null, Block: TextContentBlock block }:
                        text.Append(block.Text);
                        break;
                    case PermissionRequested request: // nothing here needs a tool
                        await session.SendAsync(new AnswerPermission(request.RequestId, PermissionDecision.Deny), patience.Token);
                        break;
                    case TurnCompleted c:
                        completed = c;
                        break;
                }
                if (completed != null) break;
            }

            Assert.NotNull(started);
            Assert.False(string.IsNullOrEmpty(started!.SessionId));
            Assert.False(string.IsNullOrEmpty(started.ClaudeVersion));
            Assert.NotNull(completed);
            Assert.False(completed!.IsError, completed.ErrorMessage);
            Assert.Contains("ready", text.ToString(), StringComparison.OrdinalIgnoreCase);
            Assert.False(session.IsBusy);
        }
    }
}
