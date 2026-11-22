using System.Linq;
using System.Reflection;
using System.Runtime.Versioning;
using Xunit;

namespace ClaudeCliProtocol.Tests
{
    /// <summary>What the package promises whatever is inside it: its two builds and no UI dependency.</summary>
    public class PackageShapeTests
    {
        private static readonly Assembly Library = Assembly.Load("ClaudeCliProtocol");

        [Fact]
        public void Each_test_runtime_loads_its_own_build_of_the_library()
        {
            var framework = Library.GetCustomAttribute<TargetFrameworkAttribute>()!.FrameworkName;
#if NET48
            Assert.Equal(".NETStandard,Version=v2.0", framework);
#else
            Assert.Equal(".NETCoreApp,Version=v6.0", framework);
#endif
        }

        [Fact]
        public void The_library_references_no_ui_package()
        {
            var names = Library.GetReferencedAssemblies().Select(a => a.Name!).ToList();
            Assert.DoesNotContain(names, n => n.StartsWith("Avalonia") || n.StartsWith("SkiaSharp") || n.StartsWith("Markdown"));
        }
    }
}
