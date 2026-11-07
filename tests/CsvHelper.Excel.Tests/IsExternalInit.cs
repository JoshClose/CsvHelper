#if !NET
namespace System.Runtime.CompilerServices;

/// <summary>Lets the compiler emit init accessors and positional records on .NET Framework, which lacks the type.</summary>
internal static class IsExternalInit
{
}
#endif
