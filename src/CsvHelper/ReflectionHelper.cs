// Copyright 2009-2015 Josh Close and Contributors
// This file is a part of CsvHelper and is dual licensed under MS-PL and Apache 2.0.
// See LICENSE.txt for details or visit http://www.opensource.org/licenses/ms-pl.html for MS-PL and http://opensource.org/licenses/Apache-2.0 for Apache 2.0.
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
		/// <param name="args">The constructor arguments.</param>
		/// <returns>A new instance of type T.</returns>
		public static T CreateInstance<T>( params object[] args )
		{
#if NET_2_0
			return Activator.CreateInstance<T>();
#else
			return (T)CreateInstance( typeof( T ), args );
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
			try
			{
				return compiled.DynamicInvoke( args );
			}
			catch( TargetInvocationException ex )
			{
				throw ex.InnerException;
			}
#endif
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
				throw new ArgumentException( "Not a constructor expression.", nameof( expression ) );
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
				throw new CsvConfigurationException( $"'{member.Name}' is not a property. Did you try to map a field by accident?" );
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
				throw new ArgumentException( "Not a member access", nameof( expression ) );
			}

			return memberExpression;
		}
#endif
	}
}
