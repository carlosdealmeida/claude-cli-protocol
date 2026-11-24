using System.Linq;
using System.Text.Json;
using Xunit;

namespace ClaudeCliProtocol.Tests
{
    public class PermissionLedgerTests
    {
        private static JsonElement J(string json)
        {
            using var doc = JsonDocument.Parse(json);
            return doc.RootElement.Clone();
        }

        private static PermissionRequested Request(string requestId, string toolUseId = "toolu_1") =>
            new PermissionRequested(requestId, "Write", "Write", "a.txt", toolUseId, J("{\"file_path\":\"a.txt\"}"), null);

        private static UserBlock ToolResult(string toolUseId) =>
            new UserBlock(null, new ToolResultBlock(toolUseId, "ok", false), null, false, false);

        private static TurnCompleted Result() => new TurnCompleted("success", false, 10, 0.01, 5, 7);

        [Fact]
        public void A_request_is_pending_until_it_is_taken()
        {
            var ledger = new PermissionLedger();
            Assert.Empty(ledger.Pending);

            Assert.True(ledger.Observe(Request("r1")));
            Assert.Equal("r1", Assert.Single(ledger.Pending).RequestId);

            Assert.True(ledger.TryTake("r1", out var taken));
            Assert.Equal("r1", taken!.RequestId);
            Assert.Empty(ledger.Pending);
        }

        [Fact]
        public void A_request_is_taken_only_once()
        {
            var ledger = new PermissionLedger();
            ledger.Observe(Request("r1"));

            Assert.True(ledger.TryTake("r1", out _));
            Assert.False(ledger.TryTake("r1", out var second));
            Assert.Null(second);
        }

        [Fact]
        public void An_unknown_request_is_not_taken()
        {
            var ledger = new PermissionLedger();
            ledger.Observe(Request("r1"));

            Assert.False(ledger.TryTake("other", out var taken));
            Assert.Null(taken);
            Assert.False(ledger.TryTake(null!, out _));
            Assert.Single(ledger.Pending);
        }

        [Fact]
        public void A_cancelled_request_leaves()
        {
            var ledger = new PermissionLedger();
            ledger.Observe(Request("r1"));
            ledger.Observe(Request("r2", "toolu_2"));

            Assert.True(ledger.Observe(new PermissionCancelled("r1")));
            Assert.Equal("r2", Assert.Single(ledger.Pending).RequestId);
            Assert.False(ledger.Observe(new PermissionCancelled("r1"))); // nothing changed the second time
            Assert.False(ledger.TryTake("r1", out _));
        }

        [Fact]
        public void A_tool_result_resolves_the_request_of_its_tool_use()
        {
            var ledger = new PermissionLedger();
            ledger.Observe(Request("r1", "toolu_1"));
            ledger.Observe(Request("r2", "toolu_2"));

            Assert.True(ledger.Observe(ToolResult("toolu_1")));
            Assert.Equal("r2", Assert.Single(ledger.Pending).RequestId);
            Assert.False(ledger.Observe(ToolResult("toolu_9"))); // a result of a tool nobody asked about
            // a user line that is not a tool result changes nothing
            Assert.False(ledger.Observe(new UserBlock(null, new TextContentBlock("hi"), null, false, false)));
            Assert.Single(ledger.Pending);
        }

        [Fact]
        public void The_end_of_the_turn_clears_everything()
        {
            var ledger = new PermissionLedger();
            ledger.Observe(Request("r1", "toolu_1"));
            ledger.Observe(Request("r2", "toolu_2"));

            Assert.True(ledger.Observe(Result()));
            Assert.Empty(ledger.Pending);
            Assert.False(ledger.Observe(Result())); // already empty
        }

        [Fact]
        public void Pending_keeps_the_order_of_arrival()
        {
            var ledger = new PermissionLedger();
            ledger.Observe(Request("b", "toolu_1"));
            ledger.Observe(Request("a", "toolu_2"));
            ledger.Observe(Request("c", "toolu_3"));

            Assert.Equal(new[] { "b", "a", "c" }, ledger.Pending.Select(p => p.RequestId).ToArray());

            // the same id again replaces the request and keeps its place
            ledger.Observe(Request("a", "toolu_9"));
            Assert.Equal(new[] { "b", "a", "c" }, ledger.Pending.Select(p => p.RequestId).ToArray());
            Assert.Equal("toolu_9", ledger.Pending[1].ToolUseId);
        }

        [Fact]
        public void Pending_is_a_copy()
        {
            var ledger = new PermissionLedger();
            ledger.Observe(Request("r1"));

            var before = ledger.Pending;
            ledger.Observe(Request("r2", "toolu_2"));

            Assert.Single(before); // the list handed out does not follow the ledger
            Assert.Equal(2, ledger.Pending.Count);
        }

        [Fact]
        public void Other_events_change_nothing()
        {
            var ledger = new PermissionLedger();
            ledger.Observe(Request("r1"));

            Assert.False(ledger.Observe(new SessionStarted("s1", "opus", "C:\\demo\\app", "default", "2.1.310")));
            Assert.False(ledger.Observe(null!));
            Assert.Single(ledger.Pending);
        }

        [Fact]
        public void Clear_empties_the_ledger()
        {
            var ledger = new PermissionLedger();
            ledger.Observe(Request("r1"));

            ledger.Clear();

            Assert.Empty(ledger.Pending);
            Assert.False(ledger.TryTake("r1", out _));
        }

        [Fact]
        public void The_taken_request_carries_the_input_and_the_suggestions_the_claude_sent()
        {
            var lines = FixtureFiles.StreamLines("edit");
            var line = lines.First(l => l.Contains("\"can_use_tool\""));
            var parser = new StreamJsonParser();
            var ledger = new PermissionLedger();
            foreach (var e in parser.Parse(line)) ledger.Observe(e);

            using var doc = JsonDocument.Parse(line);
            var requestId = doc.RootElement.GetProperty("request_id").GetString()!;
            var sent = doc.RootElement.GetProperty("request");

            Assert.True(ledger.TryTake(requestId, out var taken));
            Assert.Equal(JsonSerializer.Serialize(sent.GetProperty("input")), JsonSerializer.Serialize(taken!.Input));
            Assert.Equal(JsonSerializer.Serialize(sent.GetProperty("permission_suggestions")),
                JsonSerializer.Serialize(taken.Suggestions!.Value));
        }
    }
}
