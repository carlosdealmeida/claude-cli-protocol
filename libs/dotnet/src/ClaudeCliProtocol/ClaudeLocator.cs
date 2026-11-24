using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.InteropServices;

namespace ClaudeCliProtocol
{
    /// <summary>
    /// Finds the <c>claude</c> executable on the PATH. Only absolute folders of the PATH are searched: the current
    /// folder never is, because it is the conversation's folder, and a <c>claude</c> planted there must not be the
    /// one that runs.
    /// </summary>
    public static class ClaudeLocator
    {
        private const string Name = "claude";

        /// <summary>
        /// The full path of <c>claude</c>, or null when it is not on the PATH. On Windows it tries the extensions
        /// of PATHEXT, <c>claude.exe</c> before <c>claude.cmd</c> in the same folder; elsewhere, the bare name.
        /// </summary>
        public static string? Find() =>
            Find(Environment.GetEnvironmentVariable("PATH"), Environment.GetEnvironmentVariable("PATHEXT"),
                RuntimeInformation.IsOSPlatform(OSPlatform.Windows), File.Exists);

        internal static string? Find(string? path, string? pathExt, bool windows, Func<string, bool> isFile)
        {
            if (string.IsNullOrEmpty(path)) return null;
            var extensions = windows ? Extensions(pathExt) : new[] { "" };
            foreach (var entry in path!.Split(windows ? ';' : ':'))
            {
                var folder = entry.Trim().Trim('"');
                if (!IsAbsolute(folder, windows)) continue;
                var prefix = windows ? folder.TrimEnd('\\', '/') + "\\" : folder.TrimEnd('/') + "/";
                foreach (var extension in extensions)
                {
                    var candidate = prefix + Name + extension;
                    if (isFile(candidate)) return candidate;
                }
            }
            return null;
        }

        /// <summary>The extensions to try, <c>.exe</c> first, then those of PATHEXT in their own order.</summary>
        private static string[] Extensions(string? pathExt)
        {
            var list = new List<string> { ".exe" };
            var listed = string.IsNullOrWhiteSpace(pathExt) ? ".COM;.EXE;.BAT;.CMD" : pathExt!;
            foreach (var part in listed.Split(';'))
            {
                var extension = part.Trim().ToLowerInvariant();
                if (extension.Length > 1 && extension[0] == '.' && !list.Contains(extension)) list.Add(extension);
            }
            return list.ToArray();
        }

        /// <summary>A drive and a root (<c>C:\x</c>) or a share (<c>\\server\x</c>) on Windows; a leading slash elsewhere.</summary>
        private static bool IsAbsolute(string folder, bool windows)
        {
            if (!windows) return folder.Length > 0 && folder[0] == '/';
            if (folder.Length >= 3 && char.IsLetter(folder[0]) && folder[1] == ':' && (folder[2] == '\\' || folder[2] == '/'))
                return true;
            return folder.StartsWith(@"\\", StringComparison.Ordinal) && folder.Length > 2;
        }
    }
}
