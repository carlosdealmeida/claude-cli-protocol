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
        public void The_net6_build_sends_the_init_marker_on_to_the_runtime()
        {
            // A netstandard2.0 library compiled against this package names this library's own IsExternalInit in the
            // signature of every init setter it calls (and a `with` calls them). Under .NET 6 or later it is given the
            // net6.0 build, whose setters are signed with the runtime's type: that build has to forward the name, or
            // the setter is not found (MissingMethodException).
#if NET48
            Assert.NotNull(Library.GetType("System.Runtime.CompilerServices.IsExternalInit"));
#else
            Assert.Contains(typeof(System.Runtime.CompilerServices.IsExternalInit), Library.GetForwardedTypes());
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
