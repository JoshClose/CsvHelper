#if !NET
// The trimming attributes that .NET 5 and later have, for the .NET Standard and .NET Framework builds. The compiler
// accepts internal definitions, and the trimming analyzers and the trimmer of an application that uses these builds
// match them by name.
namespace System.Diagnostics.CodeAnalysis
{
	/// <summary>The members of a type that reflection uses; stands in for the .NET enum.</summary>
	[Flags]
	internal enum DynamicallyAccessedMemberTypes
	{
		/// <summary>No members.</summary>
		None = 0,

		/// <summary>The public parameterless constructor.</summary>
		PublicParameterlessConstructor = 0x0001,

		/// <summary>The public constructors.</summary>
		PublicConstructors = 0x0002 | PublicParameterlessConstructor,

		/// <summary>The constructors that are not public.</summary>
		NonPublicConstructors = 0x0004,

		/// <summary>The public fields.</summary>
		PublicFields = 0x0020,

		/// <summary>The fields that are not public.</summary>
		NonPublicFields = 0x0040,

		/// <summary>The public properties.</summary>
		PublicProperties = 0x0200,

		/// <summary>The properties that are not public.</summary>
		NonPublicProperties = 0x0400,
	}

	/// <summary>Says which members of a type reflection uses; stands in for the .NET attribute.</summary>
	[AttributeUsage(
		AttributeTargets.Field | AttributeTargets.ReturnValue | AttributeTargets.GenericParameter | AttributeTargets.Parameter |
		AttributeTargets.Property | AttributeTargets.Method | AttributeTargets.Class | AttributeTargets.Interface | AttributeTargets.Struct,
		Inherited = false)]
	internal sealed class DynamicallyAccessedMembersAttribute : Attribute
	{
		/// <summary>Creates the attribute.</summary>
		/// <param name="memberTypes">The members that reflection uses.</param>
		public DynamicallyAccessedMembersAttribute(DynamicallyAccessedMemberTypes memberTypes) => MemberTypes = memberTypes;

		/// <summary>The members that reflection uses.</summary>
		public DynamicallyAccessedMemberTypes MemberTypes { get; }
	}

	/// <summary>Suppresses a warning of the trimming analyzers where it does not apply; stands in for the .NET attribute.</summary>
	[AttributeUsage(AttributeTargets.All, Inherited = false, AllowMultiple = true)]
	internal sealed class UnconditionalSuppressMessageAttribute : Attribute
	{
		/// <summary>Creates the attribute.</summary>
		/// <param name="category">The category of the warning.</param>
		/// <param name="checkId">The identifier of the warning.</param>
		public UnconditionalSuppressMessageAttribute(string category, string checkId)
		{
			Category = category;
			CheckId = checkId;
		}

		/// <summary>The category of the warning.</summary>
		public string Category { get; }

		/// <summary>The identifier of the warning.</summary>
		public string CheckId { get; }

		/// <summary>Why the warning does not apply.</summary>
		public string? Justification { get; set; }
	}
}
#endif
