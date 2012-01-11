// Copyright 2009-2012 Josh Close
// This file is a part of CsvHelper and is licensed under the MS-PL
// See LICENSE.txt for details or visit http://www.opensource.org/licenses/ms-pl.html
// http://csvhelper.com
using System;
using System.ComponentModel;
using System.Linq.Expressions;
using System.Reflection;

namespace CsvHelper
{
	/// <summary>
	/// Common reflection tasks.
	/// </summary>
	public static class ReflectionHelper
	{
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
			var attributes = property.GetCustomAttributes( typeof( T ), inherit );
			if( attributes.Length > 0 )
			{
				attribute = attributes[0] as T;
			}
			return attribute;
		}

		/// <summary>
		/// Gets the <see cref="TypeConverter"/> for the <see cref="PropertyInfo"/>.
		/// </summary>
		/// <param name="property">The property to get the <see cref="TypeConverter"/> from.</param>
		/// <returns>The <see cref="TypeConverter"/> </returns>
		public static TypeConverter GetTypeConverterFromAttribute( PropertyInfo property )
		{
			TypeConverter typeConverter = null;
			var typeConverterAttribute = GetAttribute<TypeConverterAttribute>( property, false );
			if( typeConverterAttribute != null )
			{
				var typeConverterType = Type.GetType( typeConverterAttribute.ConverterTypeName, false );
				if( typeConverterType != null )
				{
					typeConverter = Activator.CreateInstance( typeConverterType ) as TypeConverter;
				}
			}
			return typeConverter;
		}

		/// <summary>
		/// Gets the property from the expression.
		/// </summary>
		/// <typeparam name="TModel">The type of the model.</typeparam>
		/// <param name="expression">The expression.</param>
		/// <returns>The <see cref="PropertyInfo"/> for the expression.</returns>
		public static PropertyInfo GetProperty<TModel>( Expression<Func<TModel, object>> expression )
		{
			return (PropertyInfo)GetMemberExpression( expression ).Member;
		}

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
	}
}
