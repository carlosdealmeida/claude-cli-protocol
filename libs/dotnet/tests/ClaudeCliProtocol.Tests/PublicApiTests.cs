using System.Collections.Generic;
using System.Linq;
using Xunit;

namespace ClaudeCliProtocol.Tests
{
    /// <summary>The names of the public API: renaming one later breaks the apps that use the package.</summary>
    public class PublicApiTests
    {
        private static readonly HashSet<string> Names =
            new HashSet<string>(typeof(AnsiText).Assembly.GetExportedTypes().Select(t => t.FullName!));

        [Fact]
        public void The_text_readers_are_public()
        {
            foreach (var name in new[] { "AnsiText", "AttachedFileLine", "TaggedMessage", "TaggedSection" })
                Assert.Contains("ClaudeCliProtocol." + name, Names);
        }

        [Fact]
        public void Helpers_stay_internal()
        {
            foreach (var name in new[] { "LightMarkup", "MarkupElement", "MarkupNode", "ContextBlock", "ContextBlockReader" })
                Assert.DoesNotContain("ClaudeCliProtocol." + name, Names);
        }

        [Fact]
        public void The_catalog_uses_the_names_of_this_library()
        {
            Assert.Contains("ClaudeCliProtocol.SlashCommand", Names);
            Assert.Contains("ClaudeCliProtocol.ModelOption", Names);
            Assert.DoesNotContain("ClaudeCliProtocol.ChatCommand", Names);
            Assert.DoesNotContain("ClaudeCliProtocol.ChatModel", Names);
            Assert.True(typeof(UsageLimits).GetProperty("IsSnapshot")!.GetMethod!.IsPublic);
        }

        [Fact]
        public void The_readers_and_the_events_are_public()
        {
            foreach (var name in new[] { "StreamJsonParser", "TranscriptParser", "ClaudeEvent", "ContentBlock",
                                         "TextContentBlock", "PermissionRequested", "TurnCompleted" })
                Assert.Contains("ClaudeCliProtocol." + name, Names);
            Assert.DoesNotContain("ClaudeCliProtocol.TextBlock", Names); // the name the CVC avoided, kept avoided
            // the 36 that came from Claude Visual Chat, ControlReplied, which the session adds, and BackgroundTasksChanged (0.2.0)
            Assert.Contains("ClaudeCliProtocol.BackgroundTasksChanged", Names);
            Assert.Equal(38, typeof(ClaudeEvent).Assembly.GetExportedTypes().Count(t => t.IsSubclassOf(typeof(ClaudeEvent))));
        }

        [Fact]
        public void The_transport_and_the_session_are_public()
        {
            foreach (var name in new[] { "IClaudeTransport", "TransportState", "ReplayTransport", "PermissionLedger",
                                         "ClaudeSession", "ControlReplied" })
                Assert.Contains("ClaudeCliProtocol." + name, Names);
        }

        [Fact]
        public void The_commands_and_their_writer_are_public()
        {
            foreach (var name in new[] { "ClaudeCommand", "ControlCommand", "SendUserMessage", "AnswerPermission",
                                         "PermissionDecision", "ImageData", "StdinWriter" })
                Assert.Contains("ClaudeCliProtocol." + name, Names);
            // the CVC keeps a StdinMessages of its own: one name in two libraries would clash in an app that uses both
            Assert.DoesNotContain("ClaudeCliProtocol.StdinMessages", Names);
        }

        [Fact]
        public void The_process_transport_is_public()
        {
            foreach (var name in new[] { "ProcessTransport", "ProcessTransportOptions", "ClaudeArguments", "ClaudeLocator" })
                Assert.Contains("ClaudeCliProtocol." + name, Names);
        }

        [Fact]
        public void Every_public_type_is_in_the_one_namespace()
        {
            Assert.All(typeof(AnsiText).Assembly.GetExportedTypes(), t => Assert.Equal("ClaudeCliProtocol", t.Namespace));
        }
    }
}
