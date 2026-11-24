using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;

namespace ClaudeCliProtocol
{
    /// <summary>
    /// The command line of <c>claude -p</c> in stream-json, and nothing else: a fixed list of arguments plus the
    /// few values a caller may choose, each one checked. No argument outside this list is ever built, and
    /// <c>--setting-sources</c> never is: the rules of each machine hold in any mode.
    /// </summary>
    public static class ClaudeArguments
    {
        /// <summary>The permission modes a transport may start in or ask for.</summary>
        public static readonly IReadOnlyList<string> PermissionModes = new[] { "plan", "default", "acceptEdits", "auto" };

        /// <summary>The effort levels.</summary>
        public static readonly IReadOnlyList<string> EffortLevels = new[] { "low", "medium", "high", "xhigh", "max" };

        // Aliases (sonnet, opus[1m]), ids (claude-opus-4-6), Bedrock profiles (arn:...:inference-profile/...) and
        // Vertex ids (...@20250514): letters, digits and . _ : @ / [ ] -, starting with a letter or a digit.
        private static readonly Regex ModelPattern =
            new Regex(@"\A[A-Za-z0-9][A-Za-z0-9._:@/\[\]-]{0,99}\z", RegexOptions.CultureInvariant);

        // The UUID the claude itself made.
        private static readonly Regex SessionPattern =
            new Regex(@"\A[A-Za-z0-9][A-Za-z0-9_-]{0,199}\z", RegexOptions.CultureInvariant);

        /// <summary>
        /// The arguments for these options, in the fixed order. Throws <see cref="ArgumentException"/> for a value
        /// that is refused: nothing must start with such a value.
        /// </summary>
        public static IReadOnlyList<string> Build(ProcessTransportOptions options)
        {
            if (options == null) throw new ArgumentNullException(nameof(options));
            if (options.WorkingDirectory == null) throw new ArgumentNullException(nameof(options), "The working directory is null.");
            if (options.WorkingDirectory.Trim().Length == 0)
                throw new ArgumentException("The working directory is empty.", nameof(options));
            if (!Contains(PermissionModes, options.PermissionMode))
                throw new ArgumentException("Permission mode refused; accepted: " + string.Join(", ", PermissionModes) + ".", nameof(options));

            var args = new List<string>
            {
                "-p", "--input-format", "stream-json", "--output-format", "stream-json", "--verbose",
                "--include-partial-messages", "--permission-prompt-tool", "stdio", "--permission-mode", options.PermissionMode,
            };
            if (options.ResumeSessionId != null)
            {
                if (!SessionPattern.IsMatch(options.ResumeSessionId))
                    throw new ArgumentException("Session id refused: only letters, digits, _ and -, starting with a letter or a digit, up to 200 characters.", nameof(options));
                args.Add("--resume");
                args.Add(options.ResumeSessionId);
            }
            if (options.Model != null)
            {
                if (!ModelPattern.IsMatch(options.Model))
                    throw new ArgumentException("Model refused: only letters, digits and . _ : @ / [ ] -, starting with a letter or a digit, up to 100 characters.", nameof(options));
                args.Add("--model");
                args.Add(options.Model);
            }
            if (options.Effort != null)
            {
                if (!Contains(EffortLevels, options.Effort))
                    throw new ArgumentException("Effort refused; accepted: " + string.Join(", ", EffortLevels) + ".", nameof(options));
                args.Add("--effort");
                args.Add(options.Effort);
            }
            return args;
        }

        private static bool Contains(IReadOnlyList<string> values, string? value)
        {
            foreach (var candidate in values)
                if (string.Equals(candidate, value, StringComparison.Ordinal)) return true;
            return false;
        }
    }
}
