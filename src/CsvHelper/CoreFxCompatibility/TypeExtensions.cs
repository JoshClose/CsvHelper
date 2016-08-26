#if !COREFX && !NET_4_5
using System;

namespace System.Reflection
{
	/// <summary>
	/// Type extensions for compatibility.
	/// </summary>
    public static class TypeExtensions
    {
		/// <summary>
		/// Gets the <see cref="TypeInfo"/> for the given <see cref="System.Type"/>.
		/// </summary>
		/// <param name="type">The <see cref="System.Type"/> to get the <see cref="TypeInfo"/> from.</param>
		/// <returns></returns>
		public static TypeInfo GetTypeInfo( this Type type )
		{
			return new TypeInfo( type );
		}
    }
}
#endif // !COREFX && !NET_4_5
