// Copyright 2009-2015 Josh Close and Contributors
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
	/// <typeparam name="T">The <see cref="Type"/> of class to map.</typeparam>
	public abstract class CsvClassMap<T> : CsvClassMap
	{
		/// <summary>
		/// Constructs the row object using the given expression.
		/// </summary>
		/// <param name="expression">The expression.</param>
		public virtual void ConstructUsing( Expression<Func<T>> expression )
		{
			Constructor = ReflectionHelper.GetConstructor( expression );
		}

		/// <summary>
		/// Maps a property to a CSV field.
		/// </summary>
		/// <param name="expression">The property to map.</param>
		/// <returns>The property mapping.</returns>
		public virtual CsvPropertyMap Map( Expression<Func<T, object>> expression )
		{
			var property = ReflectionHelper.GetProperty( expression );

			var existingMap = PropertyMaps.SingleOrDefault( m =>
				m.Data.Property == property
				|| m.Data.Property.Name == property.Name
				&& ( m.Data.Property.DeclaringType.GetTypeInfo().IsAssignableFrom( property.DeclaringType.GetTypeInfo() ) || property.DeclaringType.IsAssignableFrom( m.Data.Property.DeclaringType ) ) );
			if( existingMap != null )
			{
				return existingMap;
			}

			var propertyMap = new CsvPropertyMap( property );
			propertyMap.Data.Index = GetMaxIndex() + 1;
			PropertyMaps.Add( propertyMap );

			return propertyMap;
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

			var existingMap = ReferenceMaps.SingleOrDefault( m =>
				m.Data.Property == property
				|| m.Data.Property.Name == property.Name
				&& ( m.Data.Property.DeclaringType.IsAssignableFrom( property.DeclaringType ) || property.DeclaringType.IsAssignableFrom( m.Data.Property.DeclaringType ) ) );
			if( existingMap != null )
			{
				return existingMap;
			}

			var map = ReflectionHelper.CreateInstance<TClassMap>( constructorArgs );
			map.CreateMap();
			map.ReIndex( GetMaxIndex() + 1 );
			var reference = new CsvPropertyReferenceMap( property, map );
			ReferenceMaps.Add( reference );

			return reference;
		}

		/// <summary>
		/// Maps a property to another class map.
		/// </summary>
		/// <param name="type">The type.</param>
		/// <param name="expression">The expression.</param>
		/// <param name="constructorArgs">Constructor arguments used to create the reference map.</param>
		/// <returns>The reference mapping for the property</returns>
		[Obsolete( "This method is deprecated and will be removed in the next major release. Use References<TClassMap>( Expression<Func<T, object>> expression, params object[] constructorArgs ) instead.", false )]
		protected virtual CsvPropertyReferenceMap References( Type type, Expression<Func<T, object>> expression, params object[] constructorArgs )
		{
			var property = ReflectionHelper.GetProperty( expression );
			var existingMap = ReferenceMaps.SingleOrDefault( m =>
				m.Data.Property == property
				|| m.Data.Property.Name == property.Name
				&& ( m.Data.Property.DeclaringType.IsAssignableFrom( property.DeclaringType ) || property.DeclaringType.IsAssignableFrom( m.Data.Property.DeclaringType ) ) );
			if( existingMap != null )
			{
				return existingMap;
			}

			var map = (CsvClassMap)ReflectionHelper.CreateInstance( type, constructorArgs );
			map.CreateMap();
			map.ReIndex( GetMaxIndex() + 1 );
			var reference = new CsvPropertyReferenceMap( property, map );
			ReferenceMaps.Add( reference );

			return reference;
		}
	}
}
#endif // !NET_2_0
