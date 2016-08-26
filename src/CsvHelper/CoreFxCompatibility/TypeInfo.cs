#if !COREFX && !NET_4_5
using System;

namespace System.Reflection
{
	/// <summary>
	/// Represents type declarations for class types, interface types, 
	/// array types, value types,  enumeration types, type parameters, 
	/// generic type definitions, and open or closed constructed generic types.
	/// </summary>
	public class TypeInfo
    {
	    private readonly Type type;

		/// <summary>
		/// Gets the base type.
		/// </summary>
	    public Type BaseType => type.BaseType;

		/// <summary>
		/// Gets a value indicating if the type is a class.
		/// </summary>
	    public bool IsClass => type.IsClass;

		/// <summary>
		/// Gets a value indicating if the type is generic.
		/// </summary>
		public bool IsGenericType => type.IsGenericType;

		/// <summary>
		/// Gets a value indicating if the type is primitive.
		/// </summary>
	    public bool IsPrimitive => type.IsPrimitive;

		/// <summary>
		/// Gets a value indicating if the type is a value type.
		/// </summary>
	    public bool IsValueType => type.IsValueType;

		/// <summary>
		/// Creates a new instance for the give <see cref="System.Type"/>.
		/// </summary>
		/// <param name="type"></param>
		public TypeInfo( Type type )
	    {
		    this.type = type;
	    }

		/// <summary>
		/// Returns a value that indicates whether the specified type can be assigned to the current type.
		/// </summary>
		/// <param name="typeInfo">The type to check.</param>
		/// <returns>true if the specified type can be assigned to this type; otherwise, false.</returns>
		public bool IsAssignableFrom( TypeInfo typeInfo )
	    {
		    return type.IsAssignableFrom( typeInfo.type );
	    }
    }
}
#endif // !COREFX && !NET_4_5
