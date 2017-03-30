// Copyright 2009-2015 Josh Close and Contributors
// This file is a part of CsvHelper and is dual licensed under MS-PL and Apache 2.0.
// See LICENSE.txt for details or visit http://www.opensource.org/licenses/ms-pl.html for MS-PL and http://opensource.org/licenses/Apache-2.0 for Apache 2.0.
// http://csvhelper.com
using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
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
		private static readonly Dictionary<int, Dictionary<Type, Delegate>> funcArgCache = new Dictionary<int, Dictionary<Type, Delegate>>();

		/// <summary>
		/// Creates an instance of type T.
		/// </summary>
		/// <typeparam name="T">The type of instance to create.</typeparam>
		/// <param name="args">The constructor arguments.</param>
		/// <returns>A new instance of type T.</returns>
		public static T CreateInstance<T>( params object[] args )
		{
			return (T)CreateInstance( typeof( T ), args );
		}

		/// <summary>
		/// Creates an instance of the specified type.
		/// </summary>
		/// <param name="type">The type of instance to create.</param>
		/// <param name="args">The constructor arguments.</param>
		/// <returns>A new instance of the specified type.</returns>
		public static object CreateInstance( Type type, params object[] args )
		{
			Dictionary<Type, Delegate> funcCache;
			if( !funcArgCache.TryGetValue( args.Length, out funcCache ) )
			{
				funcArgCache[args.Length] = funcCache = new Dictionary<Type, Delegate>();
			}
			
			Delegate func;
			if( !funcCache.TryGetValue( type, out func ) )
			{
				funcCache[type] = func = CreateInstanceDelegate( type, args );
			}

			try
			{
				return func.DynamicInvoke( args );
			}
			catch( TargetInvocationException ex )
			{
				throw ex.InnerException;
			}
		}

		private static T Default<T>()
		{
			return default( T );
		}

		private static Delegate CreateInstanceDelegate( Type type, params object[] args )
		{
			Delegate compiled;
			if( type.GetTypeInfo().IsValueType )
			{
				var method = typeof( ReflectionHelper ).GetMethod( "Default", BindingFlags.Static | BindingFlags.NonPublic );
				method = method.MakeGenericMethod( type );
				compiled = Expression.Lambda( Expression.Call( method ) ).Compile();
			}
			else
			{
				var argumentTypes = args.Select( a => a.GetType() ).ToArray();
				var argumentExpressions = argumentTypes.Select( ( t, i ) => Expression.Parameter( t, "var" + i ) ).ToArray();
				var constructorInfo = type.GetConstructor( argumentTypes );
				if( constructorInfo == null )
				{
					throw new InvalidOperationException( "No public parameterless constructor found." );
				}

				var constructor = Expression.New( constructorInfo, argumentExpressions );
				compiled = Expression.Lambda( constructor, argumentExpressions ).Compile();
			}

			return compiled;
		}

		/// <summary>
		/// Gets the property from the expression.
		/// </summary>
		/// <typeparam name="TModel">The type of the model.</typeparam>
		/// <param name="expression">The expression.</param>
		/// <returns>The <see cref="PropertyInfo"/> for the expression.</returns>
		public static MemberInfo GetMember<TModel>( Expression<Func<TModel, object>> expression )
		{
			var member = GetMemberExpression( expression.Body ).Member;
			var property = member as PropertyInfo;
			if( property != null )
			{
				return property;
			}

			var field = member as FieldInfo;
			if( field != null )
			{
				return field;
			}

			throw new CsvConfigurationException( $"'{member.Name}' is not a property/field." );
		}

		/// <summary>
		/// Gets the property/field inheritance chain as a stack.
		/// </summary>
		/// <typeparam name="TModel">Type type of the model.</typeparam>
		/// <param name="expression">The member expression.</param>
		/// <returns>The inheritance chain for the given member expression as a stack.</returns>
		public static Stack<MemberInfo> GetMembers<TModel>( Expression<Func<TModel, object>> expression )
		{
			var stack = new Stack<MemberInfo>();

			var currentExpression = expression.Body;
			while( true )
			{
				var memberExpression = GetMemberExpression( currentExpression );
				if( memberExpression == null )
				{
					break;
				}

				stack.Push( memberExpression.Member );
				currentExpression = memberExpression.Expression;
			}

			return stack;
		}

		/// <summary>
		/// Gets the member expression.
		/// </summary>
		/// <param name="expression">The expression.</param>
		/// <returns></returns>
		private static MemberExpression GetMemberExpression( Expression expression )
		{
			MemberExpression memberExpression = null;
			if( expression.NodeType == ExpressionType.Convert )
			{
				var body = (UnaryExpression)expression;
				memberExpression = body.Operand as MemberExpression;
			}
			else if( expression.NodeType == ExpressionType.MemberAccess )
			{
				memberExpression = expression as MemberExpression;
			}

			return memberExpression;
		}
	}
}
