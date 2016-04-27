#if !COREFX && !NET_4_5
using System;

namespace System.Reflection
{
    public class TypeInfo
    {
	    private readonly Type type;

	    public Type BaseType => type.BaseType;

	    public bool IsClass => type.IsClass;

		public bool IsGenericType => type.IsGenericType;

	    public bool IsPrimitive => type.IsPrimitive;

	    public bool IsValueType => type.IsValueType;

		public TypeInfo( Type type )
	    {
		    this.type = type;
	    }

	    public bool IsAssignableFrom( TypeInfo typeInfo )
	    {
		    return type.IsAssignableFrom( typeInfo.type );
	    }
    }
}
#endif // !COREFX && !NET_4_5
