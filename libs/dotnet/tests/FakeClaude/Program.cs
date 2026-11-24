using System;
using System.Collections;
using System.Diagnostics;
using System.IO;
using System.Text;
using System.Text.Json;
using System.Threading;

namespace FakeClaude
{
    /// <summary>
    /// A make-believe claude. What it does, in this order:
    /// 1. FAKE_CLAUDE_SLEEP=1: writes nothing and sleeps for ever (the child of step 6).
    /// 2. Writes {"type":"fake_start","args":[...],"cwd":"...","env":{...}} with the environment variables whose
    ///    name starts with FAKE_ or CLAUDE_, or is NoDefaultCurrentDirectoryInExePath.
    /// 3. FAKE_CLAUDE_STDERR_LINES=N: writes N lines of 200 characters to stderr.
    /// 4. FAKE_CLAUDE_SCRIPT=file: writes each line of the file to stdout.
    /// 5. FAKE_CLAUDE_EXIT_NOW=N: exits with N without reading stdin.
    /// 6. FAKE_CLAUDE_CHILD=1: starts a child of itself that sleeps and inherits its stdout, and writes
    ///    {"type":"fake_child","pid":N}.
    /// 7. Reads stdin: each line comes back as {"type":"fake_stdin","line":"..."}; the line
    ///    {"type":"fake_exit","code":N} exits with N; the end of stdin exits with 0.
    /// </summary>
    internal static class Program
    {
        private static int Main(string[] args)
        {
            if (Environment.GetEnvironmentVariable("FAKE_CLAUDE_SLEEP") == "1")
            {
                Thread.Sleep(Timeout.Infinite);
                return 0;
            }

            var utf8 = new UTF8Encoding(false);
            using var output = new StreamWriter(Console.OpenStandardOutput(), utf8) { AutoFlush = true, NewLine = "\n" };
            using var input = new StreamReader(Console.OpenStandardInput(), utf8);

            output.WriteLine(Json(w =>
            {
                w.WriteString("type", "fake_start");
                w.WriteStartArray("args");
                foreach (var arg in args) w.WriteStringValue(arg);
                w.WriteEndArray();
                w.WriteString("cwd", Directory.GetCurrentDirectory());
                w.WriteStartObject("env");
                foreach (DictionaryEntry variable in Environment.GetEnvironmentVariables())
                {
                    var name = (string)variable.Key;
                    if (name.StartsWith("FAKE_", StringComparison.Ordinal) || name.StartsWith("CLAUDE_", StringComparison.Ordinal)
                        || name == "NoDefaultCurrentDirectoryInExePath")
                        w.WriteString(name, (string?)variable.Value);
                }
                w.WriteEndObject();
            }));

            if (int.TryParse(Environment.GetEnvironmentVariable("FAKE_CLAUDE_STDERR_LINES"), out var noise))
            {
                var line = new string('e', 200);
                for (var i = 0; i < noise; i++) Console.Error.WriteLine(line);
                Console.Error.Flush();
            }

            var script = Environment.GetEnvironmentVariable("FAKE_CLAUDE_SCRIPT");
            if (!string.IsNullOrEmpty(script))
                foreach (var line in File.ReadAllLines(script, utf8)) output.WriteLine(line);

            if (int.TryParse(Environment.GetEnvironmentVariable("FAKE_CLAUDE_EXIT_NOW"), out var code)) return code;

            if (Environment.GetEnvironmentVariable("FAKE_CLAUDE_CHILD") == "1")
            {
                var info = new ProcessStartInfo(Environment.ProcessPath!) { UseShellExecute = false };
                info.Environment["FAKE_CLAUDE_SLEEP"] = "1";
                info.Environment.Remove("FAKE_CLAUDE_CHILD");
                var child = Process.Start(info)!;
                output.WriteLine(Json(w =>
                {
                    w.WriteString("type", "fake_child");
                    w.WriteNumber("pid", child.Id);
                }));
            }

            string? read;
            while ((read = input.ReadLine()) != null)
            {
                if (TryExitCode(read, out var exit)) return exit;
                var echoed = read;
                output.WriteLine(Json(w =>
                {
                    w.WriteString("type", "fake_stdin");
                    w.WriteString("line", echoed);
                }));
            }
            return 0;
        }

        private static bool TryExitCode(string line, out int code)
        {
            code = 0;
            if (!line.Contains("\"fake_exit\"")) return false;
            try
            {
                using var doc = JsonDocument.Parse(line);
                var root = doc.RootElement;
                if (root.ValueKind != JsonValueKind.Object || !root.TryGetProperty("type", out var type)
                    || type.GetString() != "fake_exit") return false;
                code = root.TryGetProperty("code", out var value) ? value.GetInt32() : 0;
                return true;
            }
            catch (JsonException)
            {
                return false;
            }
        }

        private static string Json(Action<Utf8JsonWriter> body)
        {
            using var stream = new MemoryStream();
            using (var writer = new Utf8JsonWriter(stream))
            {
                writer.WriteStartObject();
                body(writer);
                writer.WriteEndObject();
            }
            return Encoding.UTF8.GetString(stream.ToArray());
        }
    }
}
