#if NET_2_0
namespace System.Runtime.CompilerServices
{
	/// <summary>
	/// Used to have extension methods in C# 2.0.
	/// </summary>
	[AttributeUsage( AttributeTargets.Assembly | AttributeTargets.Class | AttributeTargets.Method )]
	public sealed class ExtensionAttribute : Attribute
	{
	}
}
#endif
