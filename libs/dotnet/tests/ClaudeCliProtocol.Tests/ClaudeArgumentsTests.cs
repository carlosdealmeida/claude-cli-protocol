using System;
using System.Collections.Generic;
using System.Linq;
using Xunit;

namespace ClaudeCliProtocol.Tests
{
    public class ClaudeArgumentsTests
    {
        private const string Folder = @"C:\demo\app";

        private static readonly string[] Fixed =
        {
            "-p", "--input-format", "stream-json", "--output-format", "stream-json", "--verbose",
            "--include-partial-messages", "--permission-prompt-tool", "stdio", "--permission-mode",
        };

        /// <summary>Every option the transport may ever pass.</summary>
        private static readonly string[] Allowed =
        {
            "--input-format", "--output-format", "--verbose", "--include-partial-messages", "--permission-prompt-tool",
            "--permission-mode", "--resume", "--model", "--effort",
        };

        [Fact]
        public void The_fixed_arguments_come_first_in_the_checked_order()
        {
            var args = ClaudeArguments.Build(new ProcessTransportOptions(Folder));

            Assert.Equal(Fixed.Concat(new[] { "default" }).ToArray(), args.ToArray());
        }

        [Fact]
        public void Resume_model_and_effort_follow_when_asked()
        {
            var args = ClaudeArguments.Build(new ProcessTransportOptions(Folder)
            {
                PermissionMode = "plan",
                ResumeSessionId = "8c1f6a52-0000-4000-8000-000000000001",
                Model = "claude-opus-5-5[1m]",
                Effort = "high",
            });

            Assert.Equal(
                Fixed.Concat(new[]
                {
                    "plan", "--resume", "8c1f6a52-0000-4000-8000-000000000001", "--model", "claude-opus-5-5[1m]",
                    "--effort", "high",
                }).ToArray(),
                args.ToArray());
        }

        [Theory]
        [InlineData("mode", "bypassPermissions")]
        [InlineData("mode", "dontAsk")]
        [InlineData("mode", "")]
        [InlineData("mode", "plan --dangerously-skip-permissions")]
        [InlineData("mode", "Plan")]
        [InlineData("model", "sonnet & calc")]
        [InlineData("model", "-m")]
        [InlineData("model", "a b")]
        [InlineData("model", "opus\"")]
        [InlineData("model", "opus%PATH%")]
        [InlineData("model", "opus|x")]
        [InlineData("model", "opus^x")]
        [InlineData("model", "")]
        [InlineData("session", "a\" --dangerously-skip-permissions")]
        [InlineData("session", "../x")]
        [InlineData("session", "-x")]
        [InlineData("session", "a b")]
        [InlineData("session", "")]
        [InlineData("effort", "ultra")]
        [InlineData("effort", "high low")]
        [InlineData("effort", "")]
        public void Values_that_could_become_another_command_are_refused(string field, string value)
        {
            var options = new ProcessTransportOptions(Folder);
            options = field switch
            {
                "mode" => options with { PermissionMode = value },
                "model" => options with { Model = value },
                "session" => options with { ResumeSessionId = value },
                _ => options with { Effort = value },
            };

            Assert.Throws<ArgumentException>(() => ClaudeArguments.Build(options));
        }

        [Fact]
        public void A_model_longer_than_100_characters_is_refused()
        {
            Assert.Throws<ArgumentException>(() =>
                ClaudeArguments.Build(new ProcessTransportOptions(Folder) { Model = new string('a', 101) }));
            Assert.Contains(new string('a', 100),
                ClaudeArguments.Build(new ProcessTransportOptions(Folder) { Model = new string('a', 100) }));
        }

        [Theory]
        [InlineData("sonnet")]
        [InlineData("opus[1m]")]
        [InlineData("claude-opus-4-6")]
        [InlineData("arn:aws:bedrock:us-east-1:123456789012:inference-profile/us.anthropic.claude-opus")]
        [InlineData("claude-sonnet-5@20250514")]
        public void Models_the_claude_takes_are_accepted(string model)
        {
            var args = ClaudeArguments.Build(new ProcessTransportOptions(Folder) { Model = model });

            Assert.Equal(model, args[args.ToList().IndexOf("--model") + 1]);
        }

        [Fact]
        public void The_modes_and_the_effort_levels_are_the_ones_of_the_spec()
        {
            Assert.Equal(new[] { "plan", "default", "acceptEdits", "auto" }, ClaudeArguments.PermissionModes.ToArray());
            Assert.Equal(new[] { "low", "medium", "high", "xhigh", "max" }, ClaudeArguments.EffortLevels.ToArray());
            foreach (var mode in ClaudeArguments.PermissionModes)
                Assert.Contains(mode, ClaudeArguments.Build(new ProcessTransportOptions(Folder) { PermissionMode = mode }));
            foreach (var effort in ClaudeArguments.EffortLevels)
                Assert.Contains(effort, ClaudeArguments.Build(new ProcessTransportOptions(Folder) { Effort = effort }));
        }

        [Fact]
        public void No_argument_outside_the_list_is_ever_built()
        {
            var combinations = new List<ProcessTransportOptions>();
            foreach (var mode in ClaudeArguments.PermissionModes)
                foreach (var session in new[] { null, "abc-123_x" })
                    foreach (var model in new[] { null, "opus[1m]" })
                        foreach (var effort in new[] { null, "max" })
                            combinations.Add(new ProcessTransportOptions(Folder)
                            {
                                PermissionMode = mode, ResumeSessionId = session, Model = model, Effort = effort,
                            });

            foreach (var options in combinations)
            {
                var args = ClaudeArguments.Build(options);
                Assert.Equal("-p", args[0]);
                Assert.All(args.Where(a => a.StartsWith("--", StringComparison.Ordinal)), a => Assert.Contains(a, Allowed));
                Assert.DoesNotContain("--setting-sources", args);
                // nothing in an argument needs quoting, whatever shell or command interpreter is in the way
                Assert.All(args, a => Assert.Matches(@"^[A-Za-z0-9._:@/\[\]-]+$", a));
            }
        }

        [Fact]
        public void A_null_or_empty_working_directory_is_refused()
        {
            Assert.Throws<ArgumentException>(() => ClaudeArguments.Build(new ProcessTransportOptions("")));
            Assert.Throws<ArgumentException>(() => ClaudeArguments.Build(new ProcessTransportOptions("   ")));
            Assert.Throws<ArgumentNullException>(() => ClaudeArguments.Build(new ProcessTransportOptions(null!)));
            Assert.Throws<ArgumentNullException>(() => ClaudeArguments.Build(null!));
        }

        [Fact]
        public void The_options_start_with_what_the_spec_says()
        {
            var options = new ProcessTransportOptions(Folder);

            Assert.Null(options.ClaudePath);
            Assert.Equal("default", options.PermissionMode);
            Assert.Empty(options.Environment);
            Assert.True(options.SendInitialize);
        }
    }
}
