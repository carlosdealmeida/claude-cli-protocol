using System;
using Xunit;

namespace ClaudeCliProtocol.Tests
{
    /// <summary>
    /// The marker line of a file sent with a message. The asserts that need no view come from the ChatFileTests of
    /// Claude Visual Chat; the ones about the conversation and its chips stay there.
    /// </summary>
    public class AttachedFileLineTests
    {
        private static void AssertSplit(string message, string text, params string[] paths)
        {
            var split = AttachedFileLine.Split(message);
            Assert.Equal(text, split.Text);
            Assert.Equal(paths, split.Paths);
        }

        [Fact]
        public void Format_writes_the_marker_line()
        {
            Assert.Equal("[attached file: /home/demo/a.pdf]", AttachedFileLine.Format("/home/demo/a.pdf"));
            Assert.Equal(@"[attached file: C:\demo\app\b c.csv]", AttachedFileLine.Format(@"C:\demo\app\b c.csv"));
            Assert.Throws<ArgumentNullException>(() => AttachedFileLine.Format(null!));
        }

        [Fact]
        public void The_marker_lines_at_the_end_come_off_the_text()
        {
            var message = "look at these\n\n" + AttachedFileLine.Format("/home/demo/anexos/c1/report.pdf") + "\n" +
                          AttachedFileLine.Format(@"C:\demo\anexos\c1\table.csv");

            var (text, paths) = AttachedFileLine.Split(message);

            Assert.Equal("look at these", text);
            Assert.Equal(new[] { "/home/demo/anexos/c1/report.pdf", @"C:\demo\anexos\c1\table.csv" }, paths);

            // Windows line breaks, a file alone, and nothing to split
            AssertSplit("hi\r\n[attached file: /a.txt]\r\n", "hi", "/a.txt");
            AssertSplit("[attached file: /a.txt]", "", "/a.txt");
            AssertSplit("just text\n", "just text\n");
        }

        [Fact]
        public void A_message_is_written_with_one_marker_line_per_file()
        {
            // what a host does: the text, then one line per file that landed on the claude's machine
            var text = "read them\n" + AttachedFileLine.Format("/up/a.txt") + "\n" + AttachedFileLine.Format("/up/b.csv");

            var line = StdinWriter.Write(new SendUserMessage(text));

            using var doc = System.Text.Json.JsonDocument.Parse(line);
            var content = doc.RootElement.GetProperty("message").GetProperty("content").GetString()!;
            Assert.Equal("read them\n[attached file: /up/a.txt]\n[attached file: /up/b.csv]", content);
            AssertSplit(content, "read them", "/up/a.txt", "/up/b.csv");
        }
    }
}
