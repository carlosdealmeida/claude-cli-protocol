using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using Xunit;

namespace ClaudeCliProtocol.Tests
{
    public class FixturePrivacyTests
    {
        /// <summary>
        /// The prefix of the claude.ai connectors (an MCP server of whoever recorded) and the key of the result line that
        /// says when the recorder's login expires. Each one's own words (user name, company, clients, project folders) go
        /// in <c>tests/private-words.txt</c>, one per line, which git ignores: they are checked too when the file exists.
        /// </summary>
        private static readonly string[] Forbidden = { "claude_ai_", "credential_expires_at" };

        private const string ImageCache = @"C:\\Users\\demo\\AppData\\Local\\Temp\\claude\\C--demo-app\\";

        private const string PrivateWordsFile = "private-words.txt";

        /// <summary>A user folder other than demo's: <c>Users\x</c>, <c>/Users/x</c>, Claude's <c>Users-x</c>, <c>/home/x</c>.</summary>
        private static readonly Regex OtherUserFolder =
            new Regex(@"(users[\\/-]+|home[\\/]+)(?!demo\b)\w", RegexOptions.IgnoreCase);

        /// <summary>What a credential looks like: an Anthropic key or a GitHub token.</summary>
        private static readonly Regex Credential =
            new Regex(@"sk-ant-[A-Za-z0-9_-]{8,}|gh[pousr]_[A-Za-z0-9]{30,}|github_pat_[A-Za-z0-9_]{20,}", RegexOptions.CultureInvariant);

        /// <summary>An e-mail address, of anyone: no recording has had a reason to hold one.</summary>
        private static readonly Regex EmailAddress =
            new Regex(@"[A-Za-z0-9._%+-]+@[A-Za-z0-9-]+(\.[A-Za-z0-9-]+)*\.[A-Za-z]{2,}", RegexOptions.CultureInvariant);

        private static IEnumerable<string> ForbiddenWords()
        {
            var words = new List<string>(Forbidden);
            var file = Path.Combine(AppContext.BaseDirectory, PrivateWordsFile);
            if (File.Exists(file))
            {
                foreach (var line in File.ReadAllLines(file))
                {
                    var word = line.Trim();
                    if (word.Length > 0 && !word.StartsWith("#", StringComparison.Ordinal)) words.Add(word);
                }
            }
            return words;
        }

        [Fact]
        public void There_are_two_recordings_for_each_of_the_21_scenarios()
        {
            Assert.Equal(21, FixtureFiles.Scenarios.Length);
            foreach (var scenario in FixtureFiles.Scenarios)
            {
                Assert.NotEmpty(FixtureFiles.StreamLines(scenario));
                Assert.NotEmpty(FixtureFiles.TranscriptLines(scenario));
            }
        }

        [Fact]
        public void The_user_folder_pattern_finds_other_users_and_spares_demo()
        {
            foreach (var text in new[] { @"C:\\Users\\alice\\x", "C:/Users/alice", "C--Users-alice-AppData", "/home/alice/x" })
                Assert.True(OtherUserFolder.IsMatch(text), text);
            foreach (var text in new[] { @"C:\\Users\\demo\\x", "C--Users-demo-AppData", "/home/demo", "a user, at home" })
                Assert.False(OtherUserFolder.IsMatch(text), text);
        }

        [Fact]
        public void The_credential_and_address_patterns_find_what_they_look_for()
        {
            // put together here, so no scanner takes this file for a leak
            foreach (var text in new[] { "sk-" + "ant-api03-abcDEF123_-xyz", "ghp" + "_" + new string('a', 36), "github" + "_pat_" + new string('b', 30) })
                Assert.True(Credential.IsMatch(text), text);
            foreach (var text in new[] { "a task-ant-hill", "ghp_short", "sk-and-so-on" })
                Assert.False(Credential.IsMatch(text), text);
            foreach (var text in new[] { "write to someone@example.org today", "a.b-c@mail.example.com" })
                Assert.True(EmailAddress.IsMatch(text), text);
            foreach (var text in new[] { "the @mention", "a@b", "user@localhost" })
                Assert.False(EmailAddress.IsMatch(text), text);
        }

        [Fact]
        public void Where_a_find_is_tells_the_line_and_the_path_and_not_the_value()
        {
            var text = "{\"type\":\"system\"}\n{\"type\":\"user\",\"message\":{\"content\":[{\"text\":\"ask some" + "one@example.org\"}]}}\n";

            var where = Where(text, EmailAddress);

            Assert.Equal("line 2 (type user), at message.content[0].text", where);
            Assert.DoesNotContain("example", where);
        }

        /// <summary>
        /// Where the first match of a pattern is, without the match: the line, its type and the JSON path of the
        /// string that holds it. For a file that is not one JSON object per line, only the line.
        /// </summary>
        private static string Where(string text, Regex pattern)
        {
            var match = pattern.Match(text);
            if (!match.Success) return "nowhere";
            var lines = text.Split('\n');
            var offset = 0;
            for (var i = 0; i < lines.Length; i++)
            {
                if (match.Index < offset + lines[i].Length + 1)
                {
                    var head = "line " + (i + 1);
                    try
                    {
                        using (var doc = JsonDocument.Parse(lines[i]))
                        {
                            var type = Str(doc.RootElement, "type");
                            var path = PathOf(doc.RootElement, pattern, "");
                            return head + (type != null ? " (type " + type + ")" : "") + (path != null ? ", at " + path.TrimStart('.') : "");
                        }
                    }
                    catch (JsonException)
                    {
                        return head;
                    }
                }
                offset += lines[i].Length + 1;
            }
            return "somewhere";
        }

        private static string? PathOf(JsonElement element, Regex pattern, string path)
        {
            switch (element.ValueKind)
            {
                case JsonValueKind.Object:
                    foreach (var property in element.EnumerateObject())
                    {
                        if (pattern.IsMatch(property.Name)) return path + "." + "(a key)";
                        var found = PathOf(property.Value, pattern, path + "." + property.Name);
                        if (found != null) return found;
                    }
                    return null;
                case JsonValueKind.Array:
                    var index = 0;
                    foreach (var item in element.EnumerateArray())
                    {
                        var found = PathOf(item, pattern, path + "[" + index + "]");
                        if (found != null) return found;
                        index++;
                    }
                    return null;
                case JsonValueKind.String:
                    return pattern.IsMatch(element.GetString() ?? "") ? path : null;
                default:
                    return null;
            }
        }

        [Fact]
        public void Fixtures_have_no_private_data()
        {
            var files = Directory.GetFiles(FixtureFiles.Directory);
            Assert.NotEmpty(files);
            var forbidden = ForbiddenWords().ToList();
            foreach (var file in files)
            {
                var text = File.ReadAllText(file);
                foreach (var word in forbidden)
                {
                    Assert.False(
                        text.IndexOf(word, StringComparison.OrdinalIgnoreCase) >= 0,
                        Path.GetFileName(file) + " contains '" + word + "'");
                }

                var match = OtherUserFolder.Match(text);
                Assert.False(match.Success, Path.GetFileName(file) + " has a user folder other than 'demo'");
                // what was found is not shown, only where: this message goes to a public log
                Assert.False(Credential.IsMatch(text), Path.GetFileName(file) + " has what looks like a credential, " + Where(text, Credential));
                Assert.False(EmailAddress.IsMatch(text), Path.GetFileName(file) + " has an e-mail address, " + Where(text, EmailAddress));
            }
        }

        [Fact]
        public void FixtureFiles_cover_all_scenarios()
        {
            Assert.Equal(
                new[]
                {
                    "edit", "bash", "subagent", "error", "denied", "interrupted", "mode-and-model",
                    "image", "commands", "effort", "plan", "plan-auto", "plan-feedback", "queued", "hook-blocked", "queued-compact", "error-long", "error-wrapped", "denied-long", "hook-ask", "seguranca",
                },
                FixtureFiles.Scenarios);
            foreach (var scenario in FixtureFiles.Scenarios)
            {
                AssertValidJsonLines(scenario + ".stream", FixtureFiles.StreamLines(scenario), 3);
                // a message and its reply: the model does not always write a thinking block before it
                AssertValidJsonLines(scenario + ".transcript", FixtureFiles.TranscriptLines(scenario), 2);
            }
        }

        private static readonly string[] PublicCommands =
            { "compact", "context", "usage", "cost", "model", "effort", "hello" };

        [Fact]
        public void Command_lists_hold_only_the_public_names()
        {
            var lists = 0;
            foreach (var scenario in FixtureFiles.Scenarios)
            {
                foreach (var line in FixtureFiles.StreamLines(scenario))
                {
                    using (var doc = JsonDocument.Parse(line))
                    {
                        var root = doc.RootElement;
                        JsonElement commands;
                        if (Str(root, "type") == "system" && Str(root, "subtype") == "commands_changed")
                            root.TryGetProperty("commands", out commands);
                        else if (Str(root, "type") == "control_response"
                                 && root.GetProperty("response").TryGetProperty("response", out var inner)
                                 && inner.ValueKind == JsonValueKind.Object)
                            inner.TryGetProperty("commands", out commands);
                        else continue;
                        if (commands.ValueKind != JsonValueKind.Array) continue;
                        lists++;
                        foreach (var command in commands.EnumerateArray())
                            Assert.Contains(Str(command, "name"), PublicCommands);
                    }
                }
            }

            Assert.True(lists >= 3, "found only " + lists + " command lists");
        }

        [Fact]
        public void Settings_responses_hold_only_applied()
        {
            var applied = 0;
            foreach (var scenario in FixtureFiles.Scenarios)
            {
                foreach (var line in FixtureFiles.StreamLines(scenario))
                {
                    foreach (var word in new[] { "\"effective\"", "\"env\"", "\"permissions\"" })
                        Assert.False(line.Contains(word), scenario + " has " + word);
                    if (line.Contains("\"applied\"")) applied++;
                }
            }

            Assert.True(applied >= 2, "found only " + applied + " get_settings responses");
        }

        /// <summary>Re-assembles the streamed deltas of each content block (parent, message, index).</summary>
        private static List<(string ToolUseId, string Name, string Json)> ReassembleBlocks(
            string[] lines, string scenario)
        {
            var result = new List<(string, string, string)>();
            var open = new Dictionary<string, (string Id, string Name, StringBuilder Json)>();
            var messageSeq = new Dictionary<string, int>();
            foreach (var line in lines)
            {
                using (var doc = JsonDocument.Parse(line))
                {
                    var root = doc.RootElement;
                    if (Str(root, "type") != "stream_event") continue;
                    var ev = root.GetProperty("event");
                    var parent = Str(root, "parent_tool_use_id") ?? "";
                    var type = Str(ev, "type");
                    if (type == "message_start")
                    {
                        messageSeq[parent] = messageSeq.TryGetValue(parent, out var n) ? n + 1 : 1;
                        continue;
                    }

                    if (type != "content_block_start" && type != "content_block_delta"
                        && type != "content_block_stop") continue;
                    var key = parent + "|" + (messageSeq.TryGetValue(parent, out var m) ? m : 0)
                              + "|" + ev.GetProperty("index").GetInt32();
                    if (type == "content_block_start")
                    {
                        var block = ev.GetProperty("content_block");
                        if (Str(block, "type") == "tool_use")
                            open[key] = (Str(block, "id") ?? "", Str(block, "name") ?? "", new StringBuilder());
                    }
                    else if (type == "content_block_delta" && open.ContainsKey(key))
                    {
                        var delta = ev.GetProperty("delta");
                        if (Str(delta, "type") == "input_json_delta")
                            open[key].Json.Append(Str(delta, "partial_json"));
                    }
                    else if (type == "content_block_stop" && open.ContainsKey(key))
                    {
                        result.Add((open[key].Id, open[key].Name, open[key].Json.ToString()));
                        open.Remove(key);
                    }
                }
            }

            return result;
        }

        private static string? Str(JsonElement element, string name) =>
            element.ValueKind == JsonValueKind.Object && element.TryGetProperty(name, out var v)
            && v.ValueKind == JsonValueKind.String
                ? v.GetString()
                : null;

        [Fact]
        public void Reassembled_input_json_deltas_are_valid_and_private_free()
        {
            foreach (var scenario in FixtureFiles.Scenarios)
            {
                var blocks = ReassembleBlocks(FixtureFiles.StreamLines(scenario), scenario);
                foreach (var block in blocks)
                {
                    var where = scenario + " tool_use " + block.ToolUseId;
                    var json = block.Json.Length == 0 ? "{}" : block.Json;
                    using (JsonDocument.Parse(json)) { }
                    Assert.False(json.IndexOf("cvc-demo", StringComparison.OrdinalIgnoreCase) >= 0,
                        where + " has cvc-demo");
                    // Claude's own image cache under the demo user (Read of a pasted image) is public.
                    // a regex, because string.Replace with a comparison does not exist on .NET Framework (net48)
                    var withoutImageCache = Regex.Replace(json, Regex.Escape(ImageCache), "",
                        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
                    Assert.False(withoutImageCache.IndexOf("AppData", StringComparison.OrdinalIgnoreCase) >= 0,
                        where + " has AppData");
                    Assert.False(OtherUserFolder.IsMatch(json), where + " has a user folder other than 'demo'");
                }
            }
        }

        [Fact]
        public void Reassembled_deltas_equal_the_tool_use_input_of_the_assistant_line()
        {
            var compared = 0;
            foreach (var scenario in FixtureFiles.Scenarios)
            {
                var streamed = new Dictionary<string, string>();
                foreach (var block in ReassembleBlocks(FixtureFiles.StreamLines(scenario), scenario))
                    streamed[block.ToolUseId] = block.Json.Length == 0 ? "{}" : block.Json;

                foreach (var line in FixtureFiles.StreamLines(scenario))
                {
                    using (var doc = JsonDocument.Parse(line))
                    {
                        var root = doc.RootElement;
                        if (Str(root, "type") != "assistant") continue;
                        var content = root.GetProperty("message").GetProperty("content");
                        foreach (var block in content.EnumerateArray())
                        {
                            if (Str(block, "type") != "tool_use") continue;
                            var id = Str(block, "id")!;
                            // The claude CLI does not stream the messages of a subagent (parent_tool_use_id set).
                            if (Str(root, "parent_tool_use_id") != null && !streamed.ContainsKey(id)) continue;
                            Assert.True(streamed.ContainsKey(id), scenario + ": no streamed input for " + id);
                            // The CLI may add defaults to the final input (Edit gets "replace_all": false),
                            // so every streamed property must be present and equal in the assistant input.
                            var finalInput = block.GetProperty("input");
                            using (var streamedDoc = JsonDocument.Parse(streamed[id]))
                            {
                                foreach (var property in streamedDoc.RootElement.EnumerateObject())
                                {
                                    Assert.True(
                                        finalInput.TryGetProperty(property.Name, out var finalValue)
                                        && JsonEquals(finalValue, property.Value),
                                        scenario + ": streamed '" + property.Name
                                        + "' differs from assistant input for " + id);
                                }
                            }

                            compared++;
                        }
                    }
                }
            }

            Assert.True(compared >= 10, "compared only " + compared + " tool_use blocks");
        }

        private static bool JsonEquals(JsonElement a, JsonElement b)
        {
            if (a.ValueKind != b.ValueKind) return false;
            switch (a.ValueKind)
            {
                case JsonValueKind.Object:
                    var left = new Dictionary<string, JsonElement>();
                    foreach (var p in a.EnumerateObject()) left[p.Name] = p.Value;
                    var count = 0;
                    foreach (var p in b.EnumerateObject())
                    {
                        count++;
                        if (!left.TryGetValue(p.Name, out var other) || !JsonEquals(other, p.Value)) return false;
                    }

                    return count == left.Count;
                case JsonValueKind.Array:
                    var x = a.EnumerateArray().ToArray();
                    var y = b.EnumerateArray().ToArray();
                    if (x.Length != y.Length) return false;
                    for (var i = 0; i < x.Length; i++)
                        if (!JsonEquals(x[i], y[i])) return false;
                    return true;
                default:
                    return a.GetRawText() == b.GetRawText();
            }
        }

        private static void AssertValidJsonLines(string name, string[] lines, int fewest)
        {
            Assert.True(lines.Length >= fewest, name + " has fewer than " + fewest + " lines");
            foreach (var line in lines)
            {
                using (var doc = JsonDocument.Parse(line))
                {
                    Assert.Equal(JsonValueKind.Object, doc.RootElement.ValueKind);
                }
            }
        }
    }
}
