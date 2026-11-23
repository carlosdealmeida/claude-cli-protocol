using System;
using System.Linq;
using System.Text.Json;
using Xunit;

namespace ClaudeCliProtocol.Tests
{
    /// <summary>
    /// The lines written to the claude's stdin. Ported from the StdinMessagesTests of Claude Visual Chat: the JSON of
    /// each line is the same, the input is now a command instead of the view's event arguments.
    /// </summary>
    public class StdinWriterTests
    {
        private const string Tricky = "say \"hi\"\nnão \\ <b> & 'x' +1\ttab";

        private static JsonElement J(string json)
        {
            using var doc = JsonDocument.Parse(json);
            return doc.RootElement.Clone();
        }

        private static JsonElement Parse(string line)
        {
            Assert.DoesNotContain("\n", line);
            Assert.DoesNotContain("\r", line);
            Assert.True(line.All(ch => ch < 128), "non-ASCII character in " + line);
            return J(line);
        }

        private static string[] Names(JsonElement obj) => obj.EnumerateObject().Select(p => p.Name).ToArray();

        // Same structure and order, whatever the escaping.
        private static string Canonical(JsonElement element) => JsonSerializer.Serialize(element);

        /// <summary>A request as the process side received it from the claude.</summary>
        private static PermissionRequested Received(
            string requestId = "req-7",
            string input = "{\"file_path\":\"C:\\\\demo\\\\app\\\\a.txt\",\"content\":\"one\",\"replace_all\":false}",
            string? suggestions = "[{\"type\":\"setMode\",\"mode\":\"acceptEdits\",\"destination\":\"session\"}]") =>
            new PermissionRequested(requestId, "Write", "Write", "a.txt", "toolu_7", J(input),
                suggestions == null ? null : J(suggestions));

        private static string Answer(PermissionDecision decision, string? message = null, PermissionRequested? request = null)
        {
            request ??= Received();
            return StdinWriter.Write(new AnswerPermission(request.RequestId, decision, message), request);
        }

        [Fact]
        public void User_message_has_the_checked_shape()
        {
            var line = StdinWriter.Write(new SendUserMessage("hello"));

            Assert.Equal(
                "{\"type\":\"user\",\"message\":{\"role\":\"user\",\"content\":\"hello\"},\"parent_tool_use_id\":null,\"session_id\":\"default\"}",
                line);
            var root = Parse(line);
            Assert.Equal(new[] { "type", "message", "parent_tool_use_id", "session_id" }, Names(root));
        }

        [Fact]
        public void User_message_with_a_uuid_writes_it()
        {
            var line = StdinWriter.Write(new SendUserMessage(Tricky) { Uuid = "8c1f6a52-0000-4000-8000-000000000001" });

            var root = Parse(line);
            Assert.Equal(new[] { "type", "message", "parent_tool_use_id", "session_id", "uuid" }, Names(root));
            Assert.Equal("8c1f6a52-0000-4000-8000-000000000001", root.GetProperty("uuid").GetString());
            // the rest is what the text alone writes
            Assert.Equal(StdinWriter.Write(new SendUserMessage(Tricky)),
                line.Substring(0, line.IndexOf(",\"uuid\"", StringComparison.Ordinal)) + "}");

            // with images: the same blocks, with or without the uuid
            var png = new ImageData("image/png", "QUJD");
            Assert.Equal(
                Canonical(Parse(StdinWriter.Write(new SendUserMessage("oi") { Images = new[] { png } })).GetProperty("message")),
                Canonical(Parse(StdinWriter.Write(new SendUserMessage("oi") { Images = new[] { png }, Uuid = "u1" })).GetProperty("message")));
        }

        [Fact]
        public void Control_requests_have_the_checked_shape()
        {
            Assert.Equal("{\"type\":\"control_request\",\"request_id\":\"init-1\",\"request\":{\"subtype\":\"initialize\"}}",
                StdinWriter.Write(new Initialize { RequestId = "init-1" }));
            Assert.Equal("{\"type\":\"control_request\",\"request_id\":\"int-2\",\"request\":{\"subtype\":\"interrupt\"}}",
                StdinWriter.Write(new Interrupt { RequestId = "int-2" }));
            Assert.Equal(
                "{\"type\":\"control_request\",\"request_id\":\"mode-3\",\"request\":{\"subtype\":\"set_permission_mode\",\"mode\":\"acceptEdits\"}}",
                StdinWriter.Write(new SetPermissionMode("acceptEdits") { RequestId = "mode-3" }));
            Assert.Equal(
                "{\"type\":\"control_request\",\"request_id\":\"model-4\",\"request\":{\"subtype\":\"set_model\",\"model\":\"sonnet\"}}",
                StdinWriter.Write(new SetModel("sonnet") { RequestId = "model-4" }));
        }

        [Fact]
        public void Allow_sends_the_input_back()
        {
            var request = Received();
            var root = Parse(Answer(PermissionDecision.Allow, request: request));

            Assert.Equal(new[] { "type", "response" }, Names(root));
            Assert.Equal("control_response", root.GetProperty("type").GetString());
            var response = root.GetProperty("response");
            Assert.Equal(new[] { "subtype", "request_id", "response" }, Names(response));
            Assert.Equal("success", response.GetProperty("subtype").GetString());
            Assert.Equal("req-7", response.GetProperty("request_id").GetString());
            var body = response.GetProperty("response");
            Assert.Equal(new[] { "behavior", "updatedInput" }, Names(body));
            Assert.Equal("allow", body.GetProperty("behavior").GetString());
            Assert.Equal(Canonical(request.Input), Canonical(body.GetProperty("updatedInput")));
        }

        [Fact]
        public void Allow_always_adds_the_suggestions_as_updated_permissions()
        {
            var request = Received();
            var body = Parse(Answer(PermissionDecision.AllowAlways, request: request)).GetProperty("response").GetProperty("response");

            Assert.Equal(new[] { "behavior", "updatedInput", "updatedPermissions" }, Names(body));
            Assert.Equal("allow", body.GetProperty("behavior").GetString());
            Assert.Equal(Canonical(request.Input), Canonical(body.GetProperty("updatedInput")));
            Assert.Equal(Canonical(request.Suggestions!.Value), Canonical(body.GetProperty("updatedPermissions")));
        }

        [Fact]
        public void Allow_always_without_suggestions_is_a_plain_allow()
        {
            var body = Parse(Answer(PermissionDecision.AllowAlways, request: Received(suggestions: null)))
                .GetProperty("response").GetProperty("response");

            Assert.Equal(new[] { "behavior", "updatedInput" }, Names(body));
        }

        [Fact]
        public void Allow_always_with_suggestions_that_are_not_a_list_is_a_plain_allow()
        {
            var body = Parse(Answer(PermissionDecision.AllowAlways, request: Received(suggestions: "{\"type\":\"setMode\"}")))
                .GetProperty("response").GetProperty("response");

            Assert.Equal(new[] { "behavior", "updatedInput" }, Names(body));
        }

        [Fact]
        public void Deny_sends_the_message()
        {
            var root = Parse(Answer(PermissionDecision.Deny, "Do not create files."));
            var response = root.GetProperty("response");
            var body = response.GetProperty("response");

            Assert.Equal("req-7", response.GetProperty("request_id").GetString());
            Assert.Equal(new[] { "behavior", "message" }, Names(body));
            Assert.Equal("deny", body.GetProperty("behavior").GetString());
            Assert.Equal("Do not create files.", body.GetProperty("message").GetString());
        }

        [Theory]
        [InlineData(null)]
        [InlineData("")]
        [InlineData("   ")]
        public void Deny_without_text_uses_the_default_message(string? message)
        {
            var body = Parse(Answer(PermissionDecision.Deny, message)).GetProperty("response").GetProperty("response");

            Assert.Equal("The user denied this tool use.", body.GetProperty("message").GetString());
            Assert.Equal("The user denied this tool use.", StdinWriter.DefaultDenyMessage);
        }

        [Fact]
        public void Quotes_line_breaks_and_accents_are_escaped_and_come_back_equal()
        {
            var user = Parse(StdinWriter.Write(new SendUserMessage(Tricky)));
            Assert.Equal(Tricky, user.GetProperty("message").GetProperty("content").GetString());

            var deny = Parse(Answer(PermissionDecision.Deny, Tricky));
            Assert.Equal(Tricky, deny.GetProperty("response").GetProperty("response").GetProperty("message").GetString());

            var input = "{\"command\":" + JsonSerializer.Serialize(Tricky) + "}";
            var allow = Parse(Answer(PermissionDecision.Allow, request: Received(input: input)));
            Assert.Equal(Tricky,
                allow.GetProperty("response").GetProperty("response").GetProperty("updatedInput").GetProperty("command").GetString());

            var mode = Parse(StdinWriter.Write(new SetPermissionMode("não") { RequestId = "id \"1\"" }));
            Assert.Equal("id \"1\"", mode.GetProperty("request_id").GetString());
            Assert.Equal("não", mode.GetProperty("request").GetProperty("mode").GetString());
        }

        [Fact]
        public void Response_built_from_a_recorded_request_carries_what_the_claude_sent()
        {
            var parser = new StreamJsonParser();
            var request = FixtureFiles.StreamLines("mode-and-model")
                .SelectMany(parser.Parse).OfType<PermissionRequested>().First();

            var body = Parse(StdinWriter.Write(
                new AnswerPermission(request.RequestId, PermissionDecision.AllowAlways), request)).GetProperty("response");

            Assert.Equal(request.RequestId, body.GetProperty("request_id").GetString());
            var inner = body.GetProperty("response");
            Assert.EndsWith("a.txt", inner.GetProperty("updatedInput").GetProperty("file_path").GetString());
            Assert.Equal("setMode", inner.GetProperty("updatedPermissions")[0].GetProperty("type").GetString());
        }

        private static JsonElement Request(string line) => Parse(line).GetProperty("request");

        [Fact]
        public void User_message_without_images_keeps_string_content()
        {
            var content = Parse(StdinWriter.Write(new SendUserMessage("oi"))).GetProperty("message").GetProperty("content");
            Assert.Equal(JsonValueKind.String, content.ValueKind);
            Assert.Equal(StdinWriter.Write(new SendUserMessage("oi")),
                StdinWriter.Write(new SendUserMessage("oi") { Images = Array.Empty<ImageData>() }));
        }

        [Fact]
        public void User_message_with_images_puts_images_before_the_text()
        {
            var png = new ImageData("image/png", "QUJD");

            var content = Parse(StdinWriter.Write(new SendUserMessage("oi") { Images = new[] { png } }))
                .GetProperty("message").GetProperty("content");

            Assert.Equal(2, content.GetArrayLength());
            Assert.Equal("image", content[0].GetProperty("type").GetString());
            var source = content[0].GetProperty("source");
            Assert.Equal("base64", source.GetProperty("type").GetString());
            Assert.Equal("image/png", source.GetProperty("media_type").GetString());
            Assert.Equal("QUJD", source.GetProperty("data").GetString());
            Assert.Equal("text", content[1].GetProperty("type").GetString());
            Assert.Equal("oi", content[1].GetProperty("text").GetString());
        }

        [Fact]
        public void User_message_with_only_images_has_no_text_block()
        {
            var content = Parse(StdinWriter.Write(new SendUserMessage("") { Images = new[] { new ImageData("image/png", "QUJD") } }))
                .GetProperty("message").GetProperty("content");

            Assert.Equal(1, content.GetArrayLength());
            Assert.Equal("image", content[0].GetProperty("type").GetString());
        }

        [Fact]
        public void Apply_effort_sends_the_level_or_null()
        {
            var high = Request(StdinWriter.Write(new ApplyEffort("high") { RequestId = "e1" }));
            Assert.Equal("apply_flag_settings", high.GetProperty("subtype").GetString());
            Assert.Equal("high", high.GetProperty("settings").GetProperty("effortLevel").GetString());
            Assert.Equal("e1", Parse(StdinWriter.Write(new ApplyEffort("high") { RequestId = "e1" })).GetProperty("request_id").GetString());

            var cleared = Request(StdinWriter.Write(new ApplyEffort(null) { RequestId = "e1" }));
            Assert.Equal(JsonValueKind.Null, cleared.GetProperty("settings").GetProperty("effortLevel").ValueKind);
        }

        [Fact]
        public void Query_and_remote_control_requests_have_the_right_subtype()
        {
            Assert.Equal("get_settings", Request(StdinWriter.Write(new GetSettings { RequestId = "a" })).GetProperty("subtype").GetString());
            Assert.Equal("get_context_usage", Request(StdinWriter.Write(new GetContextUsage { RequestId = "b" })).GetProperty("subtype").GetString());
            Assert.Equal("get_chrome_dialog", Request(StdinWriter.Write(new GetChromeStatus { RequestId = "c" })).GetProperty("subtype").GetString());
            var remote = Request(StdinWriter.Write(new SetRemoteControl(true) { RequestId = "r1" }));
            Assert.Equal("remote_control", remote.GetProperty("subtype").GetString());
            Assert.True(remote.GetProperty("enabled").GetBoolean());
            Assert.False(Request(StdinWriter.Write(new SetRemoteControl(false) { RequestId = "r2" })).GetProperty("enabled").GetBoolean());
        }

        [Fact]
        public void Get_task_output_asks_for_the_task()
        {
            Assert.Equal("{\"type\":\"control_request\",\"request_id\":\"r1\",\"request\":{\"subtype\":\"get_task_output\",\"task_id\":\"k1\"}}",
                         StdinWriter.Write(new GetTaskOutput("k1") { RequestId = "r1" }));
        }

        [Fact]
        public void Each_control_command_names_its_subtype()
        {
            Assert.Equal("initialize", new Initialize().Subtype);
            Assert.Equal("interrupt", new Interrupt().Subtype);
            Assert.Equal("set_permission_mode", new SetPermissionMode("plan").Subtype);
            Assert.Equal("set_model", new SetModel("opus").Subtype);
            Assert.Equal("apply_flag_settings", new ApplyEffort("high").Subtype);
            Assert.Equal("get_settings", new GetSettings().Subtype);
            Assert.Equal("get_context_usage", new GetContextUsage().Subtype);
            Assert.Equal("get_task_output", new GetTaskOutput("k1").Subtype);
            Assert.Equal("remote_control", new SetRemoteControl(true).Subtype);
            Assert.Equal("get_chrome_dialog", new GetChromeStatus().Subtype);
        }

        [Fact]
        public void Null_arguments_are_rejected()
        {
            Assert.Throws<ArgumentNullException>(() => StdinWriter.Write((SendUserMessage)null!));
            Assert.Throws<ArgumentNullException>(() => StdinWriter.Write((ControlCommand)null!));
            Assert.Throws<ArgumentNullException>(() => StdinWriter.Write(null!, Received()));
            Assert.Throws<ArgumentNullException>(() =>
                StdinWriter.Write(new AnswerPermission("req-7", PermissionDecision.Allow), null!));
            Assert.Throws<ArgumentNullException>(() => StdinWriter.Write(new SendUserMessage(null!)));
        }

        [Fact]
        public void User_message_writes_the_inline_pastes_after_the_uuid_only_when_there_are_some()
        {
            var message = new SendUserMessage("see AAA and BBB") { Uuid = "u1", InlinePastes = new[] { "AAA", "BBB" } };

            var root = Parse(StdinWriter.Write(message));

            Assert.Equal(new[] { "type", "message", "parent_tool_use_id", "session_id", "uuid", "inline_pastes" }, Names(root));
            Assert.Equal(new[] { "AAA", "BBB" }, root.GetProperty("inline_pastes").EnumerateArray().Select(p => p.GetString()).ToArray());
            Assert.Equal("see AAA and BBB", root.GetProperty("message").GetProperty("content").GetString());

            // none: the line is the one it always was
            Assert.Empty(new SendUserMessage("x").InlinePastes);
            Assert.DoesNotContain("inline_pastes", StdinWriter.Write(new SendUserMessage("x") { InlinePastes = new string[0] }));
            Assert.DoesNotContain("inline_pastes", StdinWriter.Write(new SendUserMessage("x")));

            // a paste with characters that need escaping, and with images
            var png = new ImageData("image/png", "QUJD");
            var tricky = new SendUserMessage(Tricky) { Images = new[] { png }, InlinePastes = new[] { Tricky } };
            var line = Parse(StdinWriter.Write(tricky));
            Assert.Equal(Tricky, line.GetProperty("inline_pastes")[0].GetString());
        }

        [Fact]
        public void A_control_command_without_a_request_id_is_refused()
        {
            Assert.Throws<ArgumentException>(() => StdinWriter.Write(new Interrupt()));
        }

        [Fact]
        public void An_answer_for_another_request_is_refused()
        {
            Assert.Throws<ArgumentException>(() =>
                StdinWriter.Write(new AnswerPermission("req_2", PermissionDecision.Allow), Received("req_1")));
        }

        [Fact]
        public void No_public_member_takes_the_input_or_the_suggestions_from_the_caller()
        {
            var properties = typeof(AnswerPermission).GetProperties().Select(p => p.Name).ToArray();
            Assert.Equal(new[] { "RequestId", "Decision", "Message" }, properties);
        }

        [Fact]
        public void Every_line_is_ascii()
        {
            var line = StdinWriter.Write(new SendUserMessage("ação “quoted” 日本"));
            Assert.All(line, c => Assert.True(c < 128));
            Assert.Equal("ação “quoted” 日本", J(line).GetProperty("message").GetProperty("content").GetString());
        }
    }
}
