using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Xunit;

namespace ClaudeCliProtocol.Tests
{
    public class ClaudeSessionTests
    {
        private const string Init =
            "{\"type\":\"system\",\"subtype\":\"init\",\"session_id\":\"s1\",\"model\":\"claude-opus-5-5\"," +
            "\"cwd\":\"C:\\\\demo\\\\app\",\"permissionMode\":\"default\",\"claude_code_version\":\"2.1.310\"}";

        private const string Result =
            "{\"type\":\"result\",\"subtype\":\"success\",\"is_error\":false,\"duration_ms\":10," +
            "\"usage\":{\"input_tokens\":1,\"output_tokens\":2}}";

        private static string CanUseTool(string requestId, string toolUseId) =>
            "{\"type\":\"control_request\",\"request_id\":\"" + requestId + "\",\"request\":{\"subtype\":\"can_use_tool\"," +
            "\"tool_name\":\"Write\",\"tool_use_id\":\"" + toolUseId + "\",\"input\":{\"file_path\":\"a.txt\"}," +
            "\"permission_suggestions\":[{\"type\":\"setMode\",\"mode\":\"acceptEdits\",\"destination\":\"session\"}]}}";

        private static string Success(string requestId, string body = "{}") =>
            "{\"type\":\"control_response\",\"response\":{\"subtype\":\"success\",\"request_id\":\"" + requestId +
            "\",\"response\":" + body + "}}";

        private static string Error(string requestId) =>
            "{\"type\":\"control_response\",\"response\":{\"subtype\":\"error\",\"request_id\":\"" + requestId +
            "\",\"error\":\"not now\"}}";

        private static async Task<List<ClaudeEvent>> Drain(ClaudeSession session)
        {
            var events = new List<ClaudeEvent>();
            await foreach (var e in session.ReadEventsAsync()) events.Add(e);
            return events;
        }

        private static string SentId(ScriptedTransport transport, int index = -1)
        {
            var command = index < 0 ? transport.Sent.Last() : transport.Sent[index];
            return ((ControlCommand)command).RequestId!;
        }

        // ---------------------------------------------------------------- state

        [Fact]
        public async Task The_init_line_gives_the_session_id_the_model_and_the_mode()
        {
            var transport = new ScriptedTransport();
            await using var session = new ClaudeSession(transport);
            Assert.Null(session.SessionId);
            Assert.Null(session.Model);
            Assert.Null(session.PermissionMode);
            transport.Enqueue(Init);
            transport.Enqueue("{\"type\":\"system\",\"subtype\":\"status\",\"status\":null,\"permissionMode\":\"plan\"}");
            transport.Close();

            var events = await Drain(session);

            Assert.IsType<SessionStarted>(events[0]);
            Assert.Equal("s1", session.SessionId);
            Assert.Equal("claude-opus-5-5", session.Model);
            Assert.Equal("plan", session.PermissionMode); // the status line came after the init
        }

        [Fact]
        public async Task A_message_makes_the_session_busy_until_its_result()
        {
            var transport = new ScriptedTransport();
            await using var session = new ClaudeSession(transport);
            Assert.False(session.IsBusy);

            Assert.True(await session.SendAsync(new SendUserMessage("hi")));

            Assert.True(session.IsBusy);
            Assert.Equal("hi", Assert.IsType<SendUserMessage>(Assert.Single(transport.Sent)).Text);
            transport.Enqueue(Init); // the init of our own turn opens no second one
            transport.Enqueue(Result);
            transport.Close();
            var busyAtEachEvent = new List<bool>();
            await foreach (var _ in session.ReadEventsAsync()) busyAtEachEvent.Add(session.IsBusy);

            Assert.Equal(new[] { true, false }, busyAtEachEvent);
        }

        [Fact]
        public async Task Two_messages_need_two_results()
        {
            var transport = new ScriptedTransport();
            await using var session = new ClaudeSession(transport);
            await session.SendAsync(new SendUserMessage("one"));
            await session.SendAsync(new SendUserMessage("two"));
            transport.Enqueue(Result);
            transport.Enqueue(Result);
            transport.Close();

            var busyAfterEachResult = new List<bool>();
            await foreach (var e in session.ReadEventsAsync())
                if (e is TurnCompleted) busyAfterEachResult.Add(session.IsBusy);

            Assert.Equal(new[] { true, false }, busyAfterEachResult);
        }

        [Fact]
        public async Task An_init_with_no_open_turn_opens_one()
        {
            // a message that came through Remote Control: this side sent nothing, the claude starts a turn
            var transport = new ScriptedTransport();
            await using var session = new ClaudeSession(transport);
            transport.Enqueue(Init);
            transport.Enqueue(Result);
            transport.Close();

            var busyAtEachEvent = new List<bool>();
            await foreach (var _ in session.ReadEventsAsync()) busyAtEachEvent.Add(session.IsBusy);

            Assert.Equal(new[] { true, false }, busyAtEachEvent);
        }

        [Fact]
        public async Task A_result_with_no_open_turn_does_not_go_negative()
        {
            // an interrupt with the session idle also brings a result
            var transport = new ScriptedTransport();
            await using var session = new ClaudeSession(transport);
            transport.Enqueue(Result);
            transport.Enqueue(Result);
            transport.Close();

            var seen = 0;
            await foreach (var e in session.ReadEventsAsync())
            {
                if (!(e is TurnCompleted)) continue;
                seen++;
                Assert.False(session.IsBusy);
                if (seen == 1)
                {
                    await session.SendAsync(new SendUserMessage("next")); // one message still makes it busy
                    Assert.True(session.IsBusy);
                }
            }
            Assert.Equal(2, seen);
        }

        [Fact]
        public async Task A_message_that_cannot_be_sent_does_not_leave_the_session_busy()
        {
            var transport = new ScriptedTransport { FailNextSend = true };
            await using var session = new ClaudeSession(transport);

            await Assert.ThrowsAsync<IOException>(() => session.SendAsync(new SendUserMessage("hi")));

            Assert.False(session.IsBusy);
            Assert.Empty(transport.Sent);
        }

        [Fact]
        public async Task State_changed_is_raised_when_busy_changes()
        {
            var transport = new ScriptedTransport();
            await using var session = new ClaudeSession(transport);
            var busyAtEachChange = new List<bool>();
            session.StateChanged += (_, _) => busyAtEachChange.Add(session.IsBusy);

            await session.SendAsync(new SendUserMessage("hi"));
            transport.Enqueue(Result);
            transport.Close();
            await Drain(session);

            Assert.True(busyAtEachChange.First());
            Assert.False(busyAtEachChange.Last());
        }

        [Fact]
        public async Task The_state_of_the_transport_is_followed()
        {
            var transport = new ScriptedTransport();
            await using var session = new ClaudeSession(transport);
            var raised = 0;
            session.StateChanged += (_, _) => raised++;
            Assert.Equal(TransportState.Open, session.TransportState);

            transport.Raise(TransportState.Dropped);
            Assert.Equal(TransportState.Dropped, session.TransportState);
            transport.Raise(TransportState.Open);
            Assert.Equal(TransportState.Open, session.TransportState);

            Assert.Equal(2, raised);
        }

        // ---------------------------------------------------------------- permissions

        [Fact]
        public async Task A_permission_request_is_pending_until_answered()
        {
            var transport = new ScriptedTransport();
            await using var session = new ClaudeSession(transport);
            transport.Enqueue(Init);
            transport.Enqueue(CanUseTool("req_1", "toolu_1"));
            transport.Close();

            await foreach (var e in session.ReadEventsAsync())
            {
                if (!(e is PermissionRequested request)) continue;
                Assert.Equal("req_1", Assert.Single(session.PendingPermissions).RequestId);

                Assert.True(await session.SendAsync(new AnswerPermission(request.RequestId, PermissionDecision.Allow)));

                Assert.Empty(session.PendingPermissions);
            }
        }

        [Fact]
        public async Task An_answer_goes_to_the_transport_as_the_command_it_is()
        {
            var transport = new ScriptedTransport();
            await using var session = new ClaudeSession(transport);
            transport.Enqueue(CanUseTool("req_1", "toolu_1"));
            transport.Close();

            await foreach (var e in session.ReadEventsAsync())
                if (e is PermissionRequested)
                    await session.SendAsync(new AnswerPermission("req_1", PermissionDecision.Deny, "not this file"));

            // the session adds nothing: the side that holds the process fills the input in
            Assert.Equal(new AnswerPermission("req_1", PermissionDecision.Deny, "not this file"), Assert.Single(transport.Sent));
        }

        [Fact]
        public async Task An_answer_to_a_request_that_is_not_pending_sends_nothing()
        {
            var transport = new ScriptedTransport();
            await using var session = new ClaudeSession(transport);
            transport.Enqueue(CanUseTool("req_1", "toolu_1"));
            transport.Close();

            Assert.False(await session.SendAsync(new AnswerPermission("never_asked", PermissionDecision.Allow)));
            await foreach (var e in session.ReadEventsAsync())
            {
                if (!(e is PermissionRequested)) continue;
                Assert.True(await session.SendAsync(new AnswerPermission("req_1", PermissionDecision.Allow)));
                Assert.False(await session.SendAsync(new AnswerPermission("req_1", PermissionDecision.Allow)));   // the second answer
                Assert.False(await session.SendAsync(new AnswerPermission("req_1", PermissionDecision.Deny)));
            }

            Assert.Single(transport.Sent);
        }

        [Fact]
        public async Task A_request_the_claude_resolves_by_itself_cannot_be_answered()
        {
            var transport = new ScriptedTransport();
            await using var session = new ClaudeSession(transport);
            transport.Enqueue(CanUseTool("req_1", "toolu_1"));
            transport.Enqueue("{\"type\":\"control_cancel_request\",\"request_id\":\"req_1\"}");
            transport.Enqueue(CanUseTool("req_2", "toolu_2"));
            transport.Enqueue(Result);
            transport.Close();

            await foreach (var e in session.ReadEventsAsync())
            {
                if (e is PermissionCancelled)
                    Assert.False(await session.SendAsync(new AnswerPermission("req_1", PermissionDecision.Allow)));
                if (e is TurnCompleted)
                    Assert.False(await session.SendAsync(new AnswerPermission("req_2", PermissionDecision.Allow)));
            }

            Assert.Empty(transport.Sent);
        }

        // ---------------------------------------------------------------- control

        [Fact]
        public async Task A_control_command_gets_a_request_id_from_the_session()
        {
            var transport = new ScriptedTransport();
            await using var session = new ClaudeSession(transport);

            Assert.True(await session.SendAsync(new SetPermissionMode("plan")));
            Assert.True(await session.SendAsync(new SetModel("opus") { RequestId = "mine" })); // the caller's id is not used

            var first = Assert.IsType<SetPermissionMode>(transport.Sent[0]);
            Assert.Equal("plan", first.Mode);
            Assert.Matches("^[0-9a-f]{32}$", first.RequestId);
            var second = Assert.IsType<SetModel>(transport.Sent[1]);
            Assert.Matches("^[0-9a-f]{32}$", second.RequestId);
            Assert.NotEqual(first.RequestId, second.RequestId);
        }

        [Fact]
        public async Task The_mode_is_confirmed_only_by_the_reply()
        {
            var transport = new ScriptedTransport();
            await using var session = new ClaudeSession(transport);
            transport.Enqueue(Init);
            await session.SendAsync(new SetPermissionMode("plan"));
            await session.SendAsync(new SetModel("sonnet"));
            Assert.Null(session.PermissionMode); // asked, not confirmed
            transport.Enqueue(Success(SentId(transport, 0)));
            transport.Enqueue(Success(SentId(transport, 1)));
            transport.Close();

            var modes = new List<string?>();
            await foreach (var e in session.ReadEventsAsync())
                if (e is SessionStarted || e is ControlReplied) modes.Add(session.PermissionMode + "/" + session.Model);

            Assert.Equal(new[] { "default/claude-opus-5-5", "plan/claude-opus-5-5", "plan/sonnet" }, modes);
        }

        [Fact]
        public async Task An_error_reply_confirms_nothing()
        {
            var transport = new ScriptedTransport();
            await using var session = new ClaudeSession(transport);
            transport.Enqueue(Init);
            await session.SendAsync(new SetPermissionMode("auto"));
            transport.Enqueue(Error(SentId(transport)));
            transport.Close();

            var events = await Drain(session);

            Assert.Equal("default", session.PermissionMode);
            var replied = Assert.Single(events.OfType<ControlReplied>());
            Assert.True(replied.Reply.IsError);
            Assert.Equal("not now", replied.Reply.Error);
        }

        [Fact]
        public async Task The_reply_comes_back_with_its_command()
        {
            var transport = new ScriptedTransport();
            await using var session = new ClaudeSession(transport);
            await session.SendAsync(new GetContextUsage());
            var id = SentId(transport);
            transport.Enqueue(Success(id));
            transport.Close();

            var events = await Drain(session);

            var acknowledged = events.FindIndex(e => e is ControlAcknowledged);
            Assert.True(acknowledged >= 0);
            var replied = Assert.IsType<ControlReplied>(events[acknowledged + 1]); // right after the reply itself
            Assert.Equal(id, Assert.IsType<GetContextUsage>(replied.Command).RequestId);
            Assert.Same(events[acknowledged], replied.Reply);
        }

        [Fact]
        public async Task A_reply_that_arrives_before_the_send_returns_is_still_matched()
        {
            var transport = new ScriptedTransport();
            await using var session = new ClaudeSession(transport);
            var reading = Task.Run(() => Drain(session));
            transport.WaitForReader();
            transport.OnSend = command =>
            {
                // the claude answers at once: the reply is read and handled while SendAsync is still running
                transport.Enqueue(Success(((ControlCommand)command).RequestId!));
                transport.WaitForReader();
            };

            await session.SendAsync(new SetPermissionMode("plan"));
            transport.OnSend = null;

            Assert.Equal("plan", session.PermissionMode);
            transport.Close();
            Assert.Single((await reading).OfType<ControlReplied>());
        }

        [Fact]
        public async Task A_reply_nobody_here_asked_for_passes_through_alone()
        {
            var transport = new ScriptedTransport();
            await using var session = new ClaudeSession(transport);
            transport.Enqueue(Success("someone_elses"));
            transport.Close();

            var events = await Drain(session);

            Assert.Equal("someone_elses", Assert.Single(events.OfType<ControlAcknowledged>()).RequestId);
            Assert.Empty(events.OfType<ControlReplied>());
        }

        [Fact]
        public async Task A_control_command_that_cannot_be_sent_is_forgotten()
        {
            var transport = new ScriptedTransport { FailNextSend = true };
            await using var session = new ClaudeSession(transport);
            await Assert.ThrowsAsync<IOException>(() => session.SendAsync(new SetPermissionMode("plan")));

            await session.SendAsync(new SetPermissionMode("acceptEdits"));
            transport.Enqueue(Success(SentId(transport)));
            transport.Close();
            var events = await Drain(session);

            Assert.Equal("acceptEdits", session.PermissionMode);
            Assert.Single(events.OfType<ControlReplied>());
        }

        [Fact]
        public async Task An_interrupt_reply_counts_what_is_still_queued()
        {
            var transport = new ScriptedTransport();
            await using var session = new ClaudeSession(transport);
            await session.SendAsync(new SendUserMessage("one"));
            await session.SendAsync(new SendUserMessage("two"));
            await session.SendAsync(new SendUserMessage("three"));
            await session.SendAsync(new Interrupt());
            // the claude dropped one queued message and kept "a": the interrupted turn and "a" still bring a result
            transport.Enqueue(Success(SentId(transport), "{\"still_queued\":[\"a\"]}"));
            transport.Enqueue(Result);
            transport.Enqueue(Result);
            transport.Close();

            var busy = new List<bool>();
            await foreach (var e in session.ReadEventsAsync())
                if (e is ControlReplied || e is TurnCompleted) busy.Add(session.IsBusy);

            Assert.Equal(new[] { true, true, false }, busy);
        }

        // ---------------------------------------------------------------- robustness

        [Fact]
        public async Task A_line_that_is_not_an_event_does_not_stop_the_session()
        {
            var transport = new ScriptedTransport();
            await using var session = new ClaudeSession(transport);
            transport.Enqueue("");
            transport.Enqueue("not json");
            transport.Enqueue("{\"type\":\"future_type\"}");
            transport.Enqueue("{\"type\":\"assistant\",\"mess");
            transport.Enqueue("\u001b[2K{\"type\":\"system\",\"subtype\":\"init\",\"session_id\":\"s1\"}");
            transport.Enqueue(Result);
            transport.Close();

            var events = await Drain(session);

            Assert.Equal("s1", session.SessionId);
            Assert.IsType<TurnCompleted>(events.Last());
        }

        [Fact]
        public async Task When_the_transport_closes_mid_turn_the_session_is_idle_with_nothing_pending()
        {
            var transport = new ScriptedTransport();
            await using var session = new ClaudeSession(transport);
            await session.SendAsync(new SendUserMessage("hi"));
            await session.SendAsync(new GetSettings());
            var asked = SentId(transport);
            transport.Enqueue(CanUseTool("req_1", "toolu_1"));
            transport.Close(); // the process died: no result, no answer

            var lastBusy = false;
            await foreach (var _ in session.ReadEventsAsync()) lastBusy = session.IsBusy;

            Assert.True(lastBusy); // busy up to the last line
            Assert.False(session.IsBusy);
            Assert.Empty(session.PendingPermissions);
            Assert.Equal(TransportState.Closed, session.TransportState);
            Assert.False(await session.SendAsync(new AnswerPermission("req_1", PermissionDecision.Allow)));
            Assert.Equal(2, transport.Sent.Count);
            Assert.NotNull(asked);
        }

        [Fact]
        public async Task The_events_are_read_only_once()
        {
            var transport = new ScriptedTransport();
            await using var session = new ClaudeSession(transport);
            transport.Close();
            await Drain(session);

            await Assert.ThrowsAsync<InvalidOperationException>(() => Drain(session));
        }

        [Fact]
        public async Task Null_arguments_are_rejected()
        {
            Assert.Throws<ArgumentNullException>(() => new ClaudeSession(null!));
            await using var session = new ClaudeSession(new ScriptedTransport());
            await Assert.ThrowsAsync<ArgumentNullException>(() => session.SendAsync(null!));
        }

        // ---------------------------------------------------------------- recordings

        public static IEnumerable<object[]> Scenarios => FixtureFiles.Scenarios.Select(s => new object[] { s });

        [Theory]
        [MemberData(nameof(Scenarios))]
        public async Task Every_recording_plays_to_the_end(string scenario)
        {
            await using var session = new ClaudeSession(new ReplayTransport(FixtureFiles.StreamLines(scenario)));

            // the state right after the last line, before the end of the recording resets it
            var events = 0;
            var busyAtTheLastLine = true;
            var pendingAtTheLastLine = -1;
            await foreach (var _ in session.ReadEventsAsync())
            {
                events++;
                busyAtTheLastLine = session.IsBusy;
                pendingAtTheLastLine = session.PendingPermissions.Count;
            }

            Assert.True(events > 0);
            Assert.False(string.IsNullOrEmpty(session.SessionId));
            Assert.False(busyAtTheLastLine, scenario + " ends with a turn open");
            Assert.Equal(0, pendingAtTheLastLine);
        }
    }
}
