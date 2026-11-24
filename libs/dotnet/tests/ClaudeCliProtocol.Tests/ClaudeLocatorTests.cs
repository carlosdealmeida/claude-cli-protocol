using System;
using System.Collections.Generic;
using System.IO;
using Xunit;

namespace ClaudeCliProtocol.Tests
{
    public class ClaudeLocatorTests
    {
        private static Func<string, bool> Files(params string[] paths)
        {
            var set = new HashSet<string>(paths, StringComparer.OrdinalIgnoreCase);
            return set.Contains;
        }

        [Fact]
        public void On_windows_the_first_folder_of_the_path_that_has_it_wins()
        {
            var found = ClaudeLocator.Find(@"C:\empty;C:\first;C:\second", ".COM;.EXE;.BAT;.CMD", windows: true,
                Files(@"C:\first\claude.exe", @"C:\second\claude.exe"));

            Assert.Equal(@"C:\first\claude.exe", found, ignoreCase: true);
        }

        [Fact]
        public void On_windows_the_exe_comes_before_the_cmd_in_the_same_folder()
        {
            var both = Files(@"C:\npm\claude.cmd", @"C:\npm\claude.exe");

            Assert.Equal(@"C:\npm\claude.exe", ClaudeLocator.Find(@"C:\npm", ".COM;.EXE;.BAT;.CMD", true, both), ignoreCase: true);
            // whatever order PATHEXT has, and with no PATHEXT at all
            Assert.Equal(@"C:\npm\claude.exe", ClaudeLocator.Find(@"C:\npm", ".CMD;.EXE", true, both), ignoreCase: true);
            Assert.Equal(@"C:\npm\claude.exe", ClaudeLocator.Find(@"C:\npm", null, true, both), ignoreCase: true);
            // only the cmd: the npm install
            Assert.Equal(@"C:\npm\claude.cmd",
                ClaudeLocator.Find(@"C:\npm", ".COM;.EXE;.BAT;.CMD", true, Files(@"C:\npm\claude.cmd")), ignoreCase: true);
        }

        [Fact]
        public void On_windows_a_file_with_no_extension_is_not_the_claude()
        {
            Assert.Null(ClaudeLocator.Find(@"C:\npm", ".EXE;.CMD", true, Files(@"C:\npm\claude")));
        }

        [Fact]
        public void Elsewhere_the_bare_name_is_looked_up()
        {
            var found = ClaudeLocator.Find("/usr/local/bin:/home/demo/.local/bin", null, windows: false,
                Files("/home/demo/.local/bin/claude"));

            Assert.Equal("/home/demo/.local/bin/claude", found?.Replace('\\', '/'));
            Assert.Null(ClaudeLocator.Find("/usr/bin", null, false, Files("/usr/bin/claude.exe")));
        }

        [Fact]
        public void Nothing_found_gives_null()
        {
            Assert.Null(ClaudeLocator.Find(@"C:\a;C:\b", ".EXE", true, Files()));
            Assert.Null(ClaudeLocator.Find(null, ".EXE", true, Files(@"C:\a\claude.exe")));
            Assert.Null(ClaudeLocator.Find("", null, false, Files("/a/claude")));
        }

        [Fact]
        public void The_current_folder_is_not_searched()
        {
            // an empty entry and "." mean the current folder to a shell; a claude planted in the conversation's
            // folder must not be the one that runs
            var here = Directory.GetCurrentDirectory();
            var planted = Files(Path.Combine(here, "claude.exe"), Path.Combine(".", "claude.exe"), "claude.exe",
                Path.Combine(here, "claude"), Path.Combine(".", "claude"), "claude");

            Assert.Null(ClaudeLocator.Find(@";.;.\;", ".EXE", true, planted));
            Assert.Null(ClaudeLocator.Find(":.:./:", null, false, planted));
            // a relative folder is not searched either
            Assert.Null(ClaudeLocator.Find("bin", ".EXE", true, Files(Path.Combine("bin", "claude.exe"))));
        }

        [Fact]
        public void Quotes_around_a_folder_of_the_path_are_dropped()
        {
            Assert.Equal(@"C:\with space\claude.exe",
                ClaudeLocator.Find("\"C:\\with space\"", ".EXE", true, Files(@"C:\with space\claude.exe")), ignoreCase: true);
        }
    }
}
