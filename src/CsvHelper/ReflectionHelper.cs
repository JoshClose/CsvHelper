// Copyright 2009-2014 Josh Close and Contributors
// This file is a part of CsvHelper and is licensed under the MS-PL
// See LICENSE.txt for details or visit http://www.opensource.org/licenses/ms-pl.html
// http://csvhelper.com
using System;
#if !NET_2_0
using System.Linq;
using System.Linq.Expressions;
#endif
using System.Reflection;
using CsvHelper.Configuration;
using CsvHelper.TypeConversion;

namespace CsvHelper
{
	/// <summary>
	/// Common reflection tasks.
	/// </summary>
	internal static class ReflectionHelper
	{
		/// <summary>
		/// Creates an instance of type T.
		/// </summary>
		/// <typeparam name="T">The type of instance to create.</typeparam>
		/// <returns>A new instance of type T.</returns>
		public static T CreateInstance<T>()
		{
#if NET_2_0
			return Activator.CreateInstance<T>();
#else

			var constructor = Expression.New( typeof( T ) );
			var compiled = (Func<T>)Expression.Lambda( constructor ).Compile();
			return compiled();
#endif
		}

		/// <summary>
		/// Creates an instance of the specified type.
		/// </summary>
		/// <param name="type">The type of instance to create.</param>
		/// <param name="args">The constructor arguments.</param>
		/// <returns>A new instance of the specified type.</returns>
		public static object CreateInstance( Type type, params object[] args )
		{
#if NET_2_0
			return Activator.CreateInstance( type, args );
#else

			var argumentTypes = args.Select( a => a.GetType() ).ToArray();
			var argumentExpressions = argumentTypes.Select( ( t, i ) => Expression.Parameter( t, "var" + i ) ).ToArray();
			var constructorInfo = type.GetConstructor( argumentTypes );
			var constructor = Expression.New( constructorInfo, argumentExpressions );
			var compiled = Expression.Lambda( constructor, argumentExpressions ).Compile();
			return compiled.DynamicInvoke( args );
#endif
		}

		/// <summary>
		/// Checks for equality of two <see cref="PropertyInfo"/>.
		/// </summary>
		/// <param name="propInfoA">The first <see cref="PropertyInfo"/> to compare.</param>
		/// <param name="propInfoB">The second <see cref="PropertyInfo"/> to compare.</param>
		/// <returns>true if the two <see cref="PropertyInfo"/> are considered equal; otherwise, false.</returns>
		public static bool PropertyInfoAreEqual( PropertyInfo propInfoA, PropertyInfo propInfoB )
		{
			// Based on code from http://blogs.msdn.com/b/kingces/archive/2005/08/17/452774.aspx
			// And discussion here http://stackoverflow.com/questions/13615927/equality-for-net-propertyinfos
			// And code here http://stackoverflow.com/questions/4640709/how-do-i-compare-two-propertyinfos-or-methods-reliably
			if( propInfoA == propInfoB )
			{
				return true;
			}

			if( propInfoA == null || propInfoB == null )
			{
				return false;
			}

			if( MetaDataAreEqual( propInfoA, propInfoB ) && propInfoA.DeclaringType.Equals( propInfoB.DeclaringType ) )
			{
				return true;
			}

			//For interfaces
			var implA = InterfacePropertyInfoFromImplementingPropertyInfo( propInfoA );
			if( MetaDataAreEqual( implA, propInfoB ) )
				return true;

			var implB = InterfacePropertyInfoFromImplementingPropertyInfo( propInfoB );
			if( MetaDataAreEqual( implB, propInfoA ) )
				return true;

			return false;
		}

		/// <summary>
		/// Gets the <see cref="PropertyInfo"/> for an interface that implements provided <see cref="PropertyInfo"/>.
		/// </summary>
		/// <param name="implementingPropertyInfo">The implementing <see cref="PropertyInfo"/>.</param>
		/// <returns>The <see cref="PropetyInfo"/> for the interface property implemented by provided <see cref="PropertyInfo"/>.</returns>
		private static PropertyInfo InterfacePropertyInfoFromImplementingPropertyInfo( PropertyInfo implementingPropertyInfo )
		{
			var type = implementingPropertyInfo.DeclaringType;
			foreach( var iface in type.GetInterfaces() )
			{
				var map = type.GetInterfaceMap( iface );
				for( int i = 0; i < map.InterfaceMethods.Length; i++ )
				{
					var ifaceMethod = map.InterfaceMethods[i];
					var targetMethod = map.TargetMethods[i];

					if( MetaDataAreEqual( targetMethod, implementingPropertyInfo.GetGetMethod() ) || MetaDataAreEqual( targetMethod, implementingPropertyInfo.GetSetMethod() ) )
						return PropertyInfoFromGetterSetterMethodInfo( ifaceMethod );
				}
			}

			return implementingPropertyInfo;
		}

		/// <summary>
		/// Gets the <see cref="PropertyInfo"/> for a given Getter/Setter <see cref="MethodInfo"/>.
		/// </summary>
		/// <param name="method">The Getter/Setter method.</param>
		/// <returns>The <see cref="PropertyInfo"/> for the given Getter/Setter method.</returns>
		private static PropertyInfo PropertyInfoFromGetterSetterMethodInfo( MethodInfo method )
		{
			//From http://stackoverflow.com/questions/520138/finding-the-hosting-propertyinfo-from-the-methodinfo-of-getter-setter
			bool takesArg = method.GetParameters().Length == 1;
			bool hasReturn = method.ReturnType != typeof( void );
			if( takesArg == hasReturn )
				return null;
			if( takesArg )
			{
				return method.DeclaringType.GetProperties()
					.Where( prop => prop.GetSetMethod() == method ).FirstOrDefault();
			}
			else
			{
				return method.DeclaringType.GetProperties()
					.Where( prop => prop.GetGetMethod() == method ).FirstOrDefault();
			}
		}

		/// <summary>
		/// Compares two <see cref="PropertyInfo"/> instances by metadata.
		/// </summary>
		/// <param name="propInfoA">The first <see cref="PropertyInfo"/> to compare.</param>
		/// <param name="propInfoB">The second <see cref="PropertyInfo"/> to compare.</param>
		/// <returns>true if the two <see cref="PropertyInfo"/> are meta-data-equal; otherwise false.</returns>
		private static bool MetaDataAreEqual( PropertyInfo propInfoA, PropertyInfo propInfoB )
		{
			return (propInfoA.MetadataToken == propInfoB.MetadataToken && propInfoA.Module == propInfoB.Module);
		}

		/// <summary>
		/// Compares two <see cref="MethodInfo"/> instances by metadata.
		/// </summary>
		/// <param name="methodInfoA">The first <see cref="MethodInfo"/> to compare.</param>
		/// <param name="methodInfoB">The second <see cref="MethodInfo"/> to compare.</param>
		/// <returns>true if the two <see cref="MethodInfo"/> are meta-data-equal; otherwise false.</returns>
		private static bool MetaDataAreEqual( MethodInfo methodInfoA, MethodInfo methodInfoB )
		{
			return (methodInfoA.MetadataToken == methodInfoB.MetadataToken && methodInfoA.Module == methodInfoB.Module);
		}

#if !NET_2_0
		/// <summary>
		/// Gets the first attribute of type T on property.
		/// </summary>
		/// <typeparam name="T">Type of attribute to get.</typeparam>
		/// <param name="property">The <see cref="PropertyInfo" /> to get the attribute from.</param>
		/// <param name="inherit">True to search inheritance tree, otherwise false.</param>
		/// <returns>The first attribute of type T, otherwise null.</returns>
		public static T GetAttribute<T>( PropertyInfo property, bool inherit ) where T : Attribute
		{
			T attribute = null;
			var attributes = property.GetCustomAttributes( typeof( T ), inherit ).ToList();
			if( attributes.Count > 0 )
			{
				attribute = attributes[0] as T;
			}
			return attribute;
		}

		/// <summary>
		/// Gets the attributes of type T on property.
		/// </summary>
		/// <typeparam name="T">Type of attribute to get.</typeparam>
		/// <param name="property">The <see cref="PropertyInfo" /> to get the attribute from.</param>
		/// <param name="inherit">True to search inheritance tree, otherwise false.</param>
		/// <returns>The attributes of type T.</returns>
		public static T[] GetAttributes<T>( PropertyInfo property, bool inherit ) where T : Attribute
		{
			var attributes = property.GetCustomAttributes( typeof( T ), inherit );
			return attributes.Cast<T>().ToArray();
		}

		/// <summary>
		/// Gets the constructor <see cref="NewExpression"/> from the give <see cref="Expression"/>.
		/// </summary>
		/// <typeparam name="T">The <see cref="Type"/> of the object that will be constructed.</typeparam>
		/// <param name="expression">The constructor <see cref="Expression"/>.</param>
		/// <returns>A constructor <see cref="NewExpression"/>.</returns>
		/// <exception cref="System.ArgumentException">Not a constructor expression.;expression</exception>
		public static NewExpression GetConstructor<T>( Expression<Func<T>> expression )
		{
			var newExpression = expression.Body as NewExpression;
			if( newExpression == null )
			{
				throw new ArgumentException( "Not a constructor expression.", "expression" );
			}

			return newExpression;
		}

		/// <summary>
		/// Gets the property from the expression.
		/// </summary>
		/// <typeparam name="TModel">The type of the model.</typeparam>
		/// <param name="expression">The expression.</param>
		/// <returns>The <see cref="PropertyInfo"/> for the expression.</returns>
		public static PropertyInfo GetProperty<TModel>( Expression<Func<TModel, object>> expression )
		{
			var member = GetMemberExpression( expression ).Member;
			var property = member as PropertyInfo;
			if( property == null )
			{
				throw new CsvConfigurationException( string.Format( "'{0}' is not a property. Did you try to map a field by accident?", member.Name ) );
			}

			return property;
		}

		/// <summary>
		/// Gets the member expression.
		/// </summary>
		/// <typeparam name="TModel">The type of the model.</typeparam>
		/// <typeparam name="T"></typeparam>
		/// <param name="expression">The expression.</param>
		/// <returns></returns>
		private static MemberExpression GetMemberExpression<TModel, T>( Expression<Func<TModel, T>> expression )
		{
			// This method was taken from FluentNHibernate.Utils.ReflectionHelper.cs and modified.
			// http://fluentnhibernate.org/

			MemberExpression memberExpression = null;
			if( expression.Body.NodeType == ExpressionType.Convert )
			{
				var body = (UnaryExpression)expression.Body;
				memberExpression = body.Operand as MemberExpression;
			}
			else if( expression.Body.NodeType == ExpressionType.MemberAccess )
			{
				memberExpression = expression.Body as MemberExpression;
			}

			if( memberExpression == null )
			{
				throw new ArgumentException( "Not a member access", "expression" );
			}

			return memberExpression;
		}
#endif
	}
}
