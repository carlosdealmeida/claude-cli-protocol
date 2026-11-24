using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
using Xunit;

namespace ClaudeCliProtocol.Tests
{
    /// <summary>
    /// Every recording of fixtures/ against its expected result. A library in any language passes the same check:
    /// read the recording, write the events in the neutral format, compare with the expected file.
    /// To write the expected files again: set CCP_UPDATE_EXPECTED=1 and run these tests (they fail, saying so).
    /// </summary>
    public class ConformanceTests
    {
        public static IEnumerable<object[]> Scenarios => FixtureFiles.Scenarios.Select(s => new object[] { s });

        private static JsonElement J(string json)
        {
            using var doc = JsonDocument.Parse(json);
            return doc.RootElement.Clone();
        }

        private static IEnumerable<(int Line, ClaudeEvent Event)> StreamEvents(string scenario)
        {
            var parser = new StreamJsonParser(); // one per recording: it keeps the message ids between lines
            var lines = FixtureFiles.StreamLines(scenario);
            for (var i = 0; i < lines.Length; i++)
                foreach (var e in parser.Parse(lines[i]))
                    yield return (i + 1, e);
        }

        private static IEnumerable<(int Line, ClaudeEvent Event)> TranscriptEvents(string scenario)
        {
            var lines = FixtureFiles.TranscriptLines(scenario);
            for (var i = 0; i < lines.Length; i++)
                foreach (var e in TranscriptParser.Parse(lines[i]))
                    yield return (i + 1, e);
        }

        /// <summary>The fixtures folder of the repository (not the copy next to the test assembly).</summary>
        private static string RepositoryFixtures()
        {
            for (var folder = new DirectoryInfo(AppContext.BaseDirectory); folder != null; folder = folder.Parent)
            {
                var fixtures = Path.Combine(folder.FullName, "fixtures");
                if (File.Exists(Path.Combine(folder.FullName, ".gitattributes")) && Directory.Exists(fixtures)) return fixtures;
            }
            throw new DirectoryNotFoundException("The fixtures folder of the repository was not found above " + AppContext.BaseDirectory);
        }

        private static void Check(string fileName, IEnumerable<(int Line, ClaudeEvent Event)> events)
        {
            var actual = ConformanceJson.Write(events);
            if (Environment.GetEnvironmentVariable("CCP_UPDATE_EXPECTED") == "1")
            {
                File.WriteAllText(Path.Combine(RepositoryFixtures(), fileName), actual, new UTF8Encoding(false));
                Assert.Fail("Wrote " + fileName + ". Read the diff, then run again without CCP_UPDATE_EXPECTED.");
            }

            var path = Path.Combine(FixtureFiles.Directory, fileName);
            Assert.True(File.Exists(path), "There is no " + fileName + ": write it with CCP_UPDATE_EXPECTED=1.");
            var difference = ConformanceJson.FirstDifference(J(File.ReadAllText(path, Encoding.UTF8)), J(actual));
            Assert.True(difference == null, fileName + " differs at " + difference);
        }

        [Fact]
        public void The_format_follows_its_rules()
        {
            var time = new DateTimeOffset(2026, 11, 22, 10, 30, 5, 250, TimeSpan.FromHours(-3));
            var events = new (int, ClaudeEvent)[]
            {
                (1, new SessionStarted("s1", null, "C:\\demo\\app", "default", "2.1.310")
                {
                    TerminalCommands = new[] { "clear" },
                    UltrathinkAvailable = true,
                }),
                (2, new AssistantBlock("m1", null, new ToolUseBlock("toolu_1", "Write", J("{\"file_path\":\"a.txt\",\"n\":1.5}")))
                {
                    Timestamp = time,
                    Supersedes = new[] { "u0" },
                }),
                (3, new UserBlock("toolu_9", new TextContentBlock("olá"), null, false, true)
                {
                    Images = new[] { new ImageBlock("image/png", "QUJD") },
                    Origin = new MessageOrigin("peer") { Name = "other" },
                }),
                (4, new PromptCacheUsed(TimeSpan.FromMinutes(5), time)),
                (5, new BlockDelta("m1", 2, null, DeltaKind.InputJson, "{\"a\"")),
            };

            var written = J(ConformanceJson.Write(events));

            var expected = J(@"{
              ""format"": 1,
              ""events"": [
                {""line"":1,""event"":""session_started"",""fields"":{""session_id"":""s1"",""model"":null,
                  ""working_directory"":""C:\\demo\\app"",""permission_mode"":""default"",""claude_version"":""2.1.310"",
                  ""terminal_commands"":[""clear""],""fast_mode_state"":null,""ultrathink_available"":true}},
                {""line"":2,""event"":""assistant_block"",""fields"":{""message_id"":""m1"",""parent_tool_use_id"":null,
                  ""block"":{""kind"":""tool_use"",""id"":""toolu_1"",""name"":""Write"",""input"":{""n"":1.5,""file_path"":""a.txt""}},
                  ""timestamp"":""2026-11-22T13:30:05.250Z"",""thinking_duration_ms"":null,""uuid"":null,
                  ""supersedes"":[""u0""],""is_synthetic"":false}},
                {""line"":3,""event"":""user_block"",""fields"":{""parent_tool_use_id"":""toolu_9"",
                  ""block"":{""kind"":""text"",""text"":""olá""},""tool_use_result"":null,""is_meta"":false,
                  ""is_compact_summary"":true,""timestamp"":null,
                  ""images"":[{""kind"":""image"",""media_type"":""image/png"",
                    ""data"":{""length"":4,""sha256"":""d9cae0dbdbf078b2020e2abe5fcd74bc1edba83c35f6b8a86d638ed9b8d3d1f9""}}],
                  ""is_synthetic"":false,""origin"":{""kind"":""peer"",""name"":""other"",""sender_task_id"":null,""subkind"":null},
                  ""uuid"":null,""is_queued_command"":false}},
                {""line"":4,""event"":""prompt_cache_used"",""fields"":{""ttl"":300000,""at"":""2026-11-22T13:30:05.250Z""}}
              ]
            }");

            // the first four events, whole
            Assert.Equal(1, written.GetProperty("format").GetInt32());
            for (var i = 0; i < 4; i++)
            {
                var difference = ConformanceJson.FirstDifference(expected.GetProperty("events")[i], written.GetProperty("events")[i]);
                Assert.True(difference == null, "event " + (i + 1) + " differs at " + difference);
            }
            // an enum goes by its name in snake_case
            var delta = written.GetProperty("events")[4];
            Assert.Equal("block_delta", delta.GetProperty("event").GetString());
            Assert.Equal("input_json", delta.GetProperty("fields").GetProperty("kind").GetString());
            Assert.Equal(2, delta.GetProperty("fields").GetProperty("index").GetInt32());
        }

        [Fact]
        public void The_comparison_tells_where_two_documents_differ()
        {
            Assert.Null(ConformanceJson.FirstDifference(J("{\"a\":1,\"b\":[1,2]}"), J("{\"b\":[1,2.0],\"a\":1.0000000000001}")));
            Assert.Equal("$.b[1]: expected 2, actual 3", ConformanceJson.FirstDifference(J("{\"b\":[1,2]}"), J("{\"b\":[1,3]}")));
            Assert.Equal("$.b: expected 2 items, actual 1", ConformanceJson.FirstDifference(J("{\"b\":[1,2]}"), J("{\"b\":[1]}")));
            Assert.Equal("$.c: missing", ConformanceJson.FirstDifference(J("{\"c\":null}"), J("{}")));
            Assert.Equal("$.d: not expected", ConformanceJson.FirstDifference(J("{}"), J("{\"d\":1}")));
            Assert.Equal("$.e: expected \"x\", actual null", ConformanceJson.FirstDifference(J("{\"e\":\"x\"}"), J("{\"e\":null}")));
        }

        [Fact]
        public void Names_go_in_snake_case()
        {
            Assert.Equal("assistant_block", ConformanceJson.SnakeCase("AssistantBlock"));
            Assert.Equal("cost_usd", ConformanceJson.SnakeCase("CostUsd"));
            Assert.Equal("is_meta", ConformanceJson.SnakeCase("IsMeta"));
            Assert.Equal("tool_use_id", ConformanceJson.SnakeCase("ToolUseId"));
            Assert.Equal("input_json", ConformanceJson.SnakeCase("InputJson"));
        }

        [Theory]
        [MemberData(nameof(Scenarios))]
        public void The_stream_recording_gives_the_expected_events(string scenario)
        {
            Check(scenario + ".stream.expected.json", StreamEvents(scenario));
        }

        [Theory]
        [MemberData(nameof(Scenarios))]
        public void The_transcript_recording_gives_the_expected_events(string scenario)
        {
            Check(scenario + ".transcript.expected.json", TranscriptEvents(scenario));
        }

        [Fact]
        public void Every_recording_belongs_to_a_listed_scenario()
        {
            // a recording nobody listed would sit in fixtures/ with no expected result and no test reading it
            var listed = new HashSet<string>(FixtureFiles.Scenarios);
            foreach (var file in Directory.GetFiles(FixtureFiles.Directory))
            {
                var name = Path.GetFileName(file);
                foreach (var suffix in new[] { ".stream.ndjson", ".transcript.jsonl", ".stream.expected.json", ".transcript.expected.json" })
                    if (name.EndsWith(suffix, StringComparison.Ordinal))
                        Assert.Contains(name.Substring(0, name.Length - suffix.Length), listed);
            }
        }

        [Fact]
        public void Every_scenario_has_its_two_expected_files()
        {
            foreach (var scenario in FixtureFiles.Scenarios)
            {
                Assert.True(File.Exists(Path.Combine(FixtureFiles.Directory, scenario + ".stream.expected.json")), scenario);
                Assert.True(File.Exists(Path.Combine(FixtureFiles.Directory, scenario + ".transcript.expected.json")), scenario);
            }
        }
    }
}
