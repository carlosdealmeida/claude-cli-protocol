using System;
using System.IO;
using System.Threading.Tasks;
using ClaudeCliProtocol;

namespace ClaudeConsole
{
    /// <summary>
    /// A console chat with the claude of this machine. One message at a time: it sends what you type, writes the
    /// reply as it comes, names each tool call, asks before a tool runs, and goes back to you when the turn ends.
    /// An empty line ends it.
    /// </summary>
    internal static class Program
    {
        private static async Task<int> Main(string[] args)
        {
            var folder = Path.GetFullPath(args.Length > 0 ? args[0] : Directory.GetCurrentDirectory());

            ProcessTransport transport;
            try
            {
                transport = new ProcessTransport(new ProcessTransportOptions(folder));
                transport.Start();
            }
            catch (Exception e) when (e is FileNotFoundException || e is DirectoryNotFoundException || e is ArgumentException
                || e is System.ComponentModel.Win32Exception)
            {
                Console.Error.WriteLine(e.Message);
                return 1;
            }

            await using var session = new ClaudeSession(transport);
            await using var events = session.ReadEventsAsync().GetAsyncEnumerator();
            Console.WriteLine("claude in " + folder + ". An empty line ends.");

            while (true)
            {
                Console.Write("> ");
                var text = Console.ReadLine();
                if (string.IsNullOrWhiteSpace(text)) return 0;
                if (!await session.SendAsync(new SendUserMessage(text!))) break;

                var turnEnded = false;
                while (!turnEnded && await events.MoveNextAsync())
                {
                    switch (events.Current)
                    {
                        case SessionStarted started:
                            Console.WriteLine("[" + started.Model + ", " + started.PermissionMode + ", claude " + started.ClaudeVersion + "]");
                            break;
                        case BlockDelta { Kind: DeltaKind.Text, ParentToolUseId: null } delta:
                            Console.Write(delta.Text); // the reply, piece by piece
                            break;
                        case AssistantBlock { ParentToolUseId: null, Block: ToolUseBlock tool }:
                            Console.WriteLine();
                            Console.WriteLine("[tool] " + tool.Name);
                            break;
                        case PermissionRequested request:
                            await AnswerAsync(session, request);
                            break;
                        case TurnCompleted completed:
                            Console.WriteLine();
                            if (completed.IsError) Console.WriteLine("[error] " + completed.ErrorMessage);
                            turnEnded = true;
                            break;
                    }
                }
                if (!turnEnded) break; // the claude ended in the middle of the turn
            }

            Console.WriteLine("The claude ended" + (transport.ExitCode is { } code ? " with exit code " + code + "." : "."));
            return transport.ExitCode ?? 1;
        }

        /// <summary>
        /// Asks before a tool runs. The answer names only the request and the decision: the tool's input goes back
        /// to the claude as the claude sent it, from what the transport itself read.
        /// </summary>
        private static async Task AnswerAsync(ClaudeSession session, PermissionRequested request)
        {
            Console.WriteLine();
            Console.WriteLine("[permission] " + request.ToolName + ": " + request.Input.GetRawText());
            Console.Write("Allow? [y] yes, [a] always, anything else is no: ");
            var typed = (Console.ReadLine() ?? "").Trim().ToLowerInvariant();
            var decision = typed == "y" ? PermissionDecision.Allow
                : typed == "a" ? PermissionDecision.AllowAlways
                : PermissionDecision.Deny;
            await session.SendAsync(new AnswerPermission(request.RequestId, decision));
        }
    }
}
