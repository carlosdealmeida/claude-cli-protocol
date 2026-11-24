using System.Collections.Generic;

namespace ClaudeCliProtocol
{
    /// <summary>
    /// How a <c>ProcessTransport</c> starts the claude. Every value that reaches the command line is checked
    /// (<see cref="ClaudeArguments.Build"/>): on Windows a <c>claude.cmd</c> goes through <c>cmd.exe</c>, where a
    /// stray character in a model name would be another command.
    /// </summary>
    public sealed record ProcessTransportOptions(string WorkingDirectory)
    {
        private static readonly IReadOnlyDictionary<string, string?> NoVariables = new Dictionary<string, string?>();

        /// <summary>The claude executable; null finds <c>claude</c> on the PATH (<c>ClaudeLocator.Find</c>).</summary>
        public string? ClaudePath { get; init; }

        /// <summary>The permission mode the claude starts in: one of <see cref="ClaudeArguments.PermissionModes"/>.</summary>
        public string PermissionMode { get; init; } = "default";

        /// <summary>The id of a session to resume (<c>--resume</c>); null starts a new one.</summary>
        public string? ResumeSessionId { get; init; }

        /// <summary>The model (<c>--model</c>): an alias, an id or a provider's profile; null leaves the claude's own.</summary>
        public string? Model { get; init; }

        /// <summary>The effort (<c>--effort</c>): one of <see cref="ClaudeArguments.EffortLevels"/>; null leaves the model's own.</summary>
        public string? Effort { get; init; }

        /// <summary>
        /// Variables added to this machine's environment for the claude alone; a null value removes the variable.
        /// <c>CLAUDE_CONFIG_DIR</c>, which selects the account, goes here. The transport knows no variable by name.
        /// </summary>
        public IReadOnlyDictionary<string, string?> Environment { get; init; } = NoVariables;

        /// <summary>Sends <c>initialize</c> as soon as the process starts, as the official SDKs do. True by default.</summary>
        public bool SendInitialize { get; init; } = true;
    }
}
