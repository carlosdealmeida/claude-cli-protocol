#if NETSTANDARD2_0
// Polyfill so records and init-only setters compile on netstandard2.0.
namespace System.Runtime.CompilerServices
{
    internal static class IsExternalInit
    {
    }
}
#else
// A netstandard2.0 library compiled against this package names the polyfill above in the signature of every init
// setter it calls. Run under .NET 6 or later, it is given this build instead, so the name is sent on to the runtime's
// own type; without it those setters are not found (MissingMethodException).
[assembly: System.Runtime.CompilerServices.TypeForwardedTo(typeof(System.Runtime.CompilerServices.IsExternalInit))]
#endif
