// Copyright 2009-2017 Josh Close and Contributors
// This file is a part of CsvHelper and is dual licensed under MS-PL and Apache 2.0.
// See LICENSE.txt for details or visit http://www.opensource.org/licenses/ms-pl.html for MS-PL and http://opensource.org/licenses/Apache-2.0 for Apache 2.0.
// https://github.com/JoshClose/CsvHelper
using System;
using System.Reflection;
using System.Linq.Expressions;

namespace CsvHelper
{
	/// <summary>
	/// Extensions to help with reflection.
	/// </summary>
    public static class ReflectionExtensions
    {
		/// <summary>
		/// Gets the type from the property/field.
		/// </summary>
		/// <param name="member">The member to get the type from.</param>
		/// <returns>The type.</returns>
	    public static Type MemberType( this MemberInfo member )
	    {
		    var property = member as PropertyInfo;
		    if( property != null )
		    {
			    return property.PropertyType;
		    }

		    var field = member as FieldInfo;
		    if( field != null )
		    {
			    return field.FieldType;
		    }

		    throw new InvalidOperationException( "Member is not a property or a field." );
	    }
		
		/// <summary>
		/// Gets a member expression for the property/field.
		/// </summary>
		/// <param name="member">The member to get the expression for.</param>
		/// <param name="expression">The member expression.</param>
		/// <returns>The member expression.</returns>
		public static MemberExpression GetMemberExpression( this MemberInfo member, Expression expression )
	    {
			var property = member as PropertyInfo;
			if( property != null )
			{
				return Expression.Property( expression, property );
			}

			var field = member as FieldInfo;
			if( field != null )
			{
				return Expression.Field( expression, field );
			}

			throw new InvalidOperationException( "Member is not a property or a field." );
		}
	}
}
