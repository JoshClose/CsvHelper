﻿// Copyright 2009-2015 Josh Close and Contributors
// This file is a part of CsvHelper and is dual licensed under MS-PL and Apache 2.0.
// See LICENSE.txt for details or visit http://www.opensource.org/licenses/ms-pl.html for MS-PL and http://opensource.org/licenses/Apache-2.0 for Apache 2.0.
// http://csvhelper.com
#if !NET_2_0
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;
using CsvHelper.TypeConversion;

namespace CsvHelper.Configuration
{
	/// <summary>
	/// Maps class properties to CSV fields.
	/// </summary>
	/// <typeparam name="T">The <see cref="System.Type"/> of class to map.</typeparam>
	public abstract class CsvClassMap<T> : CsvClassMap
	{
		/// <summary>
		/// Constructs the row object using the given expression.
		/// </summary>
		/// <param name="expression">The expression.</param>
		public virtual void ConstructUsing( Expression<Func<T>> expression )
		{
			if( !( expression.Body is NewExpression ) && !( expression.Body is MemberInitExpression ) )
			{
				throw new ArgumentException( "Not a constructor expression.", nameof( expression ) );
			}

			Constructor = expression.Body;
		}

		/// <summary>
		/// Maps a property to a CSV field.
		/// </summary>
		/// <param name="expression">The property to map.</param>
		/// <param name="useExistingMap">If true, an existing map will be used if available.
		/// If false, a new map is created for the same property.</param>
		/// <returns>The property mapping.</returns>
		public virtual CsvPropertyMap Map( Expression<Func<T, object>> expression, bool useExistingMap = true )
		{
			var stack = ReflectionHelper.GetProperties( expression );
			if( stack.Count == 0 )
			{
				throw new InvalidOperationException( "No properties were found in expression '{expression}'." );
			}

			CsvClassMap currentClassMap = this;
			PropertyInfo property;

			if( stack.Count > 1 )
			{
				// We need to add a reference map for every sub property.
				while( stack.Count > 1 )
				{
					property = stack.Pop();
					var mapType = typeof( DefaultCsvClassMap<> ).MakeGenericType( property.PropertyType );
					var referenceMap = currentClassMap.References( mapType, property );
					currentClassMap = referenceMap.Data.Mapping;
				}
			}

			// Add the property map to the last reference map.
			property = stack.Pop();

			return currentClassMap.Map( property, useExistingMap );
		}

		/// <summary>
		/// Maps a property to another class map.
		/// </summary>
		/// <typeparam name="TClassMap">The type of the class map.</typeparam>
		/// <param name="expression">The expression.</param>
		/// <param name="constructorArgs">Constructor arguments used to create the reference map.</param>
		/// <returns>The reference mapping for the property.</returns>
		public virtual CsvPropertyReferenceMap References<TClassMap>( Expression<Func<T, object>> expression, params object[] constructorArgs ) where TClassMap : CsvClassMap
		{
			var property = ReflectionHelper.GetProperty( expression );
			return References( typeof( TClassMap ), property, constructorArgs );
		}
	}
}
#endif // !NET_2_0
