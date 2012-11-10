using System;
using System.Linq;
using System.Reflection;

namespace CsvHelper.MissingFromRt45
{
	/// <summary>
	/// 
	/// </summary>
	public static class ReflectionExtensions
	{
		/// <summary>
		/// Gets the property.
		/// </summary>
		/// <param name="type">The type.</param>
		/// <param name="name">The <see cref="string"/> containing the name of the public property to get.</param>
		/// <param name="types">An array of <see cref="Type"/> objects representing the number, order, and type of 
		/// the parameters for the indexed property to get.-or- An empty array of the type System.Type (that is, 
		/// Type[] types = new Type[0]) to get a property that is not indexed.</param>
		/// <returns>A System.Reflection.PropertyInfo object representing the public property whose parameters match the specified argument types, if found; otherwise, null.</returns>
		public static PropertyInfo GetProperty( this Type type, string name, Type[] types )
		{
			return ( from property in type.GetRuntimeProperties()
					 where property.Name == name
					 where property.GetIndexParameters().Select( p => p.ParameterType ).SequenceEqual( types )
					 select property ).SingleOrDefault();

		}

		/// <summary>
		/// Searches for the public method with the specified name.
		/// </summary>
		/// <param name="type">The type.</param>
		/// <param name="name">The System.String containing the name of the public method to get.</param>
		/// <returns>A System.Reflection.MethodInfo object representing the public method with the specified name, if found; otherwise, null.</returns>
		public static MethodInfo GetMethod( this Type type, string name )
		{
			return type.GetRuntimeMethod( name, null );
		}

		/// <summary>
		/// Searches for the specified public method whose parameters match the specified argument types.
		/// </summary>
		/// <param name="type">The type.</param>
		/// <param name="name">The System.String containing the name of the public method to get.</param>
		/// <param name="types">An array of System.Type objects representing the number, order, and type of the parameters for the method to get.-or- An empty array of System.Type objects (as provided by the System.Type.EmptyTypes field) to get a method that takes no parameters.</param>
		/// <returns></returns>
		public static MethodInfo GetMethod( this Type type, string name, Type[] types )
		{
			return ( from method in type.GetRuntimeMethods()
			         where method.Name == name
					 where method.GetParameters().Select( p => p.ParameterType ).SequenceEqual( types )
			         select method ).SingleOrDefault();
		}

		/// <summary>
		/// Returns all the public properties of the current System.Type.
		/// </summary>
		/// <param name="type">The type.</param>
		/// <returns>An array of System.Reflection.PropertyInfo objects representing all public properties of the current System.Type.-or- An empty array of type System.Reflection.PropertyInfo, if the current System.Type does not have public properties.</returns>
		public static PropertyInfo[] GetProperties( this Type type )
		{
			return type.GetRuntimeProperties().ToArray();
		}

		/// <summary>
		/// Determines whether an instance of the current <see cref="Type"/> can be assigned from an instance of the specified Type.
		/// </summary>
		/// <param name="type">The type.</param>
		/// <param name="c">The Type to compare with the current Type.</param>
		/// <returns>true if c and the current Type represent the same type, or if the current Type is in the inheritance hierarchy of c, or if the current Type is an interface that c implements, or if c is a generic type parameter and the current Type represents one of the constraints of c. false if none of these conditions are true, or if c is null.</returns>
		public static bool IsAssignableFrom( this Type type, Type c )
		{
			return type.GetTypeInfo().IsAssignableFrom( c.GetTypeInfo() );
		}

		/// <summary>
		/// Returns the public get accessor for this property.
		/// </summary>
		/// <param name="property">The property.</param>
		/// <returns>A MethodInfo object representing the public get accessor for this property, or null if the get accessor is non-public or does not exist.</returns>
		public static MethodInfo GetGetMethod( this PropertyInfo property )
		{
			return property.GetMethod;
		}
	}
}
