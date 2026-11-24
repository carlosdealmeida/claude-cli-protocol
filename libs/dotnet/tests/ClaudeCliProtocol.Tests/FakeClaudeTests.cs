using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
using Xunit;

namespace ClaudeCliProtocol.Tests
{
    /// <summary>The make-believe claude itself, started with Process and no library code: the transport tests rely on it.</summary>
    public class FakeClaudeTests
    {
        private static Process Start(string arguments, string folder, params (string Name, string Value)[] environment)
        {
            var info = new ProcessStartInfo(FakeClaudeHost.Path, arguments)
            {
                WorkingDirectory = folder,
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardInput = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                StandardOutputEncoding = Encoding.UTF8,
            };
            foreach (var (name, value) in environment) info.EnvironmentVariables[name] = value;
            var process = Process.Start(info)!;
            process.ErrorDataReceived += (_, _) => { };
            process.BeginErrorReadLine();
            return process;
        }

        private static JsonElement Line(Process process)
        {
            var line = process.StandardOutput.ReadLine();
            Assert.NotNull(line);
            using var doc = JsonDocument.Parse(line!);
            return doc.RootElement.Clone();
        }

        [Fact]
        public void It_reports_its_arguments_its_folder_and_the_chosen_environment()
        {
            var folder = FakeClaudeHost.TempFolder();
            using var process = Start("-p --verbose --model opus[1m]", folder,
                ("FAKE_ONE", "1"), ("CLAUDE_CONFIG_DIR", "somewhere"), ("OTHER_SECRET", "hidden"), ("FAKE_CLAUDE_EXIT_NOW", "0"));

            var start = Line(process);
            process.WaitForExit();

            Assert.Equal("fake_start", start.GetProperty("type").GetString());
            Assert.Equal(new[] { "-p", "--verbose", "--model", "opus[1m]" },
                start.GetProperty("args").EnumerateArray().Select(a => a.GetString()).ToArray());
            Assert.Equal(Path.GetFullPath(folder).TrimEnd(Path.DirectorySeparatorChar),
                Path.GetFullPath(start.GetProperty("cwd").GetString()!).TrimEnd(Path.DirectorySeparatorChar), ignoreCase: true);
            var env = start.GetProperty("env");
            Assert.Equal("1", env.GetProperty("FAKE_ONE").GetString());
            Assert.Equal("somewhere", env.GetProperty("CLAUDE_CONFIG_DIR").GetString());
            Assert.False(env.TryGetProperty("OTHER_SECRET", out _));
        }

        [Fact]
        public void It_plays_its_script_and_echoes_what_it_reads()
        {
            var script = FakeClaudeHost.Script("{\"type\":\"system\",\"subtype\":\"init\"}", "{\"n\":2,\"text\":\"ação\"}");
            using var process = Start("-p", FakeClaudeHost.TempFolder(), ("FAKE_CLAUDE_SCRIPT", script));

            Assert.Equal("fake_start", Line(process).GetProperty("type").GetString());
            Assert.Equal("init", Line(process).GetProperty("subtype").GetString());
            Assert.Equal("ação", Line(process).GetProperty("text").GetString());

            process.StandardInput.Write("{\"hello\":1}\n");
            process.StandardInput.Flush();
            var echo = Line(process);
            Assert.Equal("fake_stdin", echo.GetProperty("type").GetString());
            Assert.Equal("{\"hello\":1}", echo.GetProperty("line").GetString());

            process.StandardInput.Close(); // the end of stdin ends it
            process.WaitForExit();
            Assert.Equal(0, process.ExitCode);
        }

        [Fact]
        public void It_exits_with_the_code_it_is_told()
        {
            using var now = Start("-p", FakeClaudeHost.TempFolder(), ("FAKE_CLAUDE_EXIT_NOW", "3"));
            now.WaitForExit();
            Assert.Equal(3, now.ExitCode);

            using var later = Start("-p", FakeClaudeHost.TempFolder());
            Line(later);
            later.StandardInput.Write("{\"type\":\"fake_exit\",\"code\":7}\n");
            later.StandardInput.Flush();
            later.WaitForExit();
            Assert.Equal(7, later.ExitCode);
        }
    }
}
