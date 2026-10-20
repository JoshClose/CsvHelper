#if !NET
// The attributes the compiler needs for required members (RecordTests), which .NET Framework lacks.
namespace System.Runtime.CompilerServices
{
	[AttributeUsage(AttributeTargets.Class | AttributeTargets.Struct | AttributeTargets.Field | AttributeTargets.Property, Inherited = false)]
	internal sealed class RequiredMemberAttribute : Attribute
	{
	}

	[AttributeUsage(AttributeTargets.All, AllowMultiple = true, Inherited = false)]
	internal sealed class CompilerFeatureRequiredAttribute(string featureName) : Attribute
	{
		public string FeatureName { get; } = featureName;
	}
}

namespace System.Diagnostics.CodeAnalysis
{
	[AttributeUsage(AttributeTargets.Constructor, Inherited = false)]
	internal sealed class SetsRequiredMembersAttribute : Attribute
	{
	}
}
#endif
