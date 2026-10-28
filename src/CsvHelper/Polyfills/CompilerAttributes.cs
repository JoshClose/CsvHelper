#if !NET
// Attributes the C# compiler needs for features this library uses (init accessors and records, [SkipLocalsInit],
// caller argument expressions). The compiler accepts internal definitions.
namespace System.Runtime.CompilerServices
{
	internal static class IsExternalInit
	{
	}

	[AttributeUsage(AttributeTargets.Module | AttributeTargets.Class | AttributeTargets.Struct | AttributeTargets.Interface |
					AttributeTargets.Constructor | AttributeTargets.Method | AttributeTargets.Property | AttributeTargets.Event,
		Inherited = false)]
	internal sealed class SkipLocalsInitAttribute : Attribute
	{
	}

	[AttributeUsage(AttributeTargets.Parameter, AllowMultiple = false, Inherited = false)]
	internal sealed class CallerArgumentExpressionAttribute(string parameterName) : Attribute
	{
		/// <summary>The name of the parameter whose argument expression the compiler passes as this parameter's value.</summary>
		public string ParameterName { get; } = parameterName;
	}
}
#endif
