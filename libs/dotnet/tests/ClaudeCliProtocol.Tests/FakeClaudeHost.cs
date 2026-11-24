using System;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;

namespace ClaudeCliProtocol.Tests
{
    /// <summary>
    /// The make-believe claude of the transport tests (tests/FakeClaude), copied next to the test assembly. It shows
    /// the arguments and the environment it got, plays a script and echoes every line it reads on stdin.
    /// </summary>
    internal static class FakeClaudeHost
    {
        /// <summary>The executable: FakeClaude.exe on Windows, FakeClaude elsewhere.</summary>
        public static string Path { get; } = System.IO.Path.Combine(AppContext.BaseDirectory, "fake-claude",
            RuntimeInformation.IsOSPlatform(OSPlatform.Windows) ? "FakeClaude.exe" : "FakeClaude");

        /// <summary>Writes the lines to a new temporary file and gives its path, for FAKE_CLAUDE_SCRIPT.</summary>
        public static string Script(params string[] lines)
        {
            var file = System.IO.Path.Combine(TempFolder(), "script.ndjson");
            File.WriteAllText(file, string.Join("\n", lines) + "\n", new UTF8Encoding(false));
            return file;
        }

        /// <summary>A new empty folder under the system's temporary one.</summary>
        public static string TempFolder()
        {
            var folder = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "ccp-tests", Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(folder);
            return folder;
        }
    }
}
