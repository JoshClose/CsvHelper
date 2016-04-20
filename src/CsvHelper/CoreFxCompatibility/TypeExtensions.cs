#if !COREFX
using System;

namespace System.Reflection
{
    public static class TypeExtensions
    {
		public static TypeInfo GetTypeInfo( this Type type )
		{
			return new TypeInfo( type );
		}
    }
}
#endif // !COREFX
