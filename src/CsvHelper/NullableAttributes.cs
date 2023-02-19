#if NET45 || NET47 || NETSTANDARD2_0
namespace System.Diagnostics.CodeAnalysis
{
	[AttributeUsage(AttributeTargets.Parameter, Inherited = false)]
	internal sealed class NotNullWhenAttribute : Attribute
	{
		/// <summary>Initializes the attribute with the specified return value condition.</summary>
		/// <param name="returnValue">
		/// The return value condition. If the method returns this value, the associated parameter will not be null.
		/// </param>
		public NotNullWhenAttribute(bool returnValue) => ReturnValue = returnValue;

		/// <summary>Gets the return value condition.</summary>
		public bool ReturnValue { get; }
	}

	[AttributeUsage(AttributeTargets.Parameter, Inherited = false)]
	internal sealed class MaybeNullWhenAttribute : Attribute
	{
		/// <summary>Initializes the attribute with the specified return value condition.</summary>
		/// <param name="returnValue">
		/// The return value condition. If the method returns this value, the associated parameter may be null.
		/// </param>
		public MaybeNullWhenAttribute(bool returnValue) => ReturnValue = returnValue;

		/// <summary>Gets the return value condition.</summary>
		public bool ReturnValue { get; }
	}

	[AttributeUsage(AttributeTargets.Field | AttributeTargets.Parameter | AttributeTargets.Property | AttributeTargets.ReturnValue, Inherited = false)]
	internal sealed class NotNullAttribute : Attribute { }

	[AttributeUsage(AttributeTargets.Method, Inherited = false)]
	internal sealed class DoesNotReturnAttribute : Attribute { }
}
#endif
#if NET45 || NET47 || NETSTANDARD2_0 || NETSTANDARD2_1
namespace System.Diagnostics.CodeAnalysis
{
	[AttributeUsage(AttributeTargets.Method | AttributeTargets.Property, Inherited = false, AllowMultiple = true)]
	internal sealed class MemberNotNullAttribute : Attribute
	{
		/// <summary>Initializes the attribute with a field or property member.</summary>
		/// <param name="member">
		/// The field or property member that is promised to be not-null.
		/// </param>
		public MemberNotNullAttribute(string member) => Members = new[] { member };

		/// <summary>Initializes the attribute with the list of field and property members.</summary>
		/// <param name="members">
		/// The list of field and property members that are promised to be not-null.
		/// </param>
		public MemberNotNullAttribute(params string[] members) => Members = members;

		/// <summary>Gets field or property member names.</summary>
		public string[] Members { get; }
	}
}
#endif
