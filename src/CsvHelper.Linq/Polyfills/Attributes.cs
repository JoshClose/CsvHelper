#if !NET
// The attributes that .NET 5 and later have, for the .NET Standard and .NET Framework builds. The compiler accepts
// internal definitions, and the trimming analyzers and the trimmer of an application that uses these builds match
// them by name, so that its warnings point at the calls into the query provider.
namespace System.Diagnostics.CodeAnalysis
{
	/// <summary>Says that a member needs code that trimming may remove; stands in for the .NET attribute.</summary>
	[AttributeUsage(AttributeTargets.Method | AttributeTargets.Constructor | AttributeTargets.Class, Inherited = false)]
	internal sealed class RequiresUnreferencedCodeAttribute : Attribute
	{
		/// <summary>Creates the attribute.</summary>
		/// <param name="message">Why the member needs code that trimming may remove.</param>
		public RequiresUnreferencedCodeAttribute(string message) => Message = message;

		/// <summary>Why the member needs code that trimming may remove.</summary>
		public string Message { get; }

		/// <summary>A link to more about it, or <see langword="null"/>.</summary>
		public string? Url { get; set; }
	}

	/// <summary>Says that a member generates code at run time; stands in for the .NET attribute.</summary>
	[AttributeUsage(AttributeTargets.Method | AttributeTargets.Constructor | AttributeTargets.Class, Inherited = false)]
	internal sealed class RequiresDynamicCodeAttribute : Attribute
	{
		/// <summary>Creates the attribute.</summary>
		/// <param name="message">Why the member generates code at run time.</param>
		public RequiresDynamicCodeAttribute(string message) => Message = message;

		/// <summary>Why the member generates code at run time.</summary>
		public string Message { get; }

		/// <summary>A link to more about it, or <see langword="null"/>.</summary>
		public string? Url { get; set; }
	}

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

		/// <summary>The public properties.</summary>
		PublicProperties = 0x0200,

		/// <summary>The public fields.</summary>
		PublicFields = 0x0020,
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

namespace System.Runtime.CompilerServices
{
	/// <summary>Lets the compiler emit <see langword="init"/> accessors and records.</summary>
	internal static class IsExternalInit
	{
	}

	/// <summary>Lets the compiler pass the expression of an argument as a string.</summary>
	[AttributeUsage(AttributeTargets.Parameter, AllowMultiple = false, Inherited = false)]
	internal sealed class CallerArgumentExpressionAttribute : Attribute
	{
		/// <summary>Creates the attribute.</summary>
		/// <param name="parameterName">The parameter whose argument expression is passed.</param>
		public CallerArgumentExpressionAttribute(string parameterName) => ParameterName = parameterName;

		/// <summary>The parameter whose argument expression is passed.</summary>
		public string ParameterName { get; }
	}
}
#endif

#if !NET9_0_OR_GREATER
namespace System.Runtime.CompilerServices
{
	/// <summary>
	/// Makes the compiler prefer an overload over others that are as good (C# 13); stands in for the .NET 9 attribute.
	/// </summary>
	[AttributeUsage(AttributeTargets.Method | AttributeTargets.Constructor | AttributeTargets.Property, AllowMultiple = false, Inherited = false)]
	internal sealed class OverloadResolutionPriorityAttribute : Attribute
	{
		/// <summary>Creates the attribute.</summary>
		/// <param name="priority">The priority; higher wins.</param>
		public OverloadResolutionPriorityAttribute(int priority) => Priority = priority;

		/// <summary>The priority; higher wins.</summary>
		public int Priority { get; }
	}
}
#endif
