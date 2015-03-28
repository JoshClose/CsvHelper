// Copyright 2009-2015 Josh Close and Contributors
// This file is a part of CsvHelper and is dual licensed under MS-PL and Apache 2.0.
// See LICENSE.txt for details or visit http://www.opensource.org/licenses/ms-pl.html for MS-PL and http://opensource.org/licenses/Apache-2.0 for Apache 2.0.
// http://csvhelper.com
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
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
		protected virtual void ConstructUsing( Expression<Func<T>> expression )
		{
			Constructor = ReflectionHelper.GetConstructor( expression );
		}

		/// <summary>
		/// Maps a property to a CSV field.
		/// </summary>
		/// <param name="expression">The property to map.</param>
		/// <returns>The property mapping.</returns>
		protected virtual CsvPropertyMap Map( Expression<Func<T, object>> expression )
		{
			var property = ReflectionHelper.GetProperty( expression );

			var existingMap = PropertyMaps.SingleOrDefault( m =>
				m.Data.Property == property
				|| m.Data.Property.Name == property.Name
				&& ( m.Data.Property.DeclaringType.IsAssignableFrom( property.DeclaringType ) || property.DeclaringType.IsAssignableFrom( m.Data.Property.DeclaringType ) ) );
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
		/// <returns>The reference mapping for the property.</returns>
		protected virtual CsvPropertyReferenceMap References<TClassMap>( Expression<Func<T, object>> expression ) where TClassMap : CsvClassMap
		{
			return References( typeof( TClassMap ), expression );
		}

		/// <summary>
		/// Maps a property to another class map.
		/// </summary>
		/// <param name="type">The type.</param>
		/// <param name="expression">The expression.</param>
		/// <returns>The reference mapping for the property</returns>
		protected virtual CsvPropertyReferenceMap References( Type type, Expression<Func<T, object>> expression )
		{
            var map = (CsvClassMap)ReflectionHelper.CreateInstance( type );
            return References(map, expression);
		}

        /// <summary>
        /// Maps a property to another class map.
        /// </summary>
        /// <param name="map">The class map.</param>
        /// <param name="expression">The expression.</param>
        /// <returns>The reference mapping for the property</returns>
        protected virtual CsvPropertyReferenceMap References( CsvClassMap map, Expression<Func<T, object>> expression )
		{
			var property = ReflectionHelper.GetProperty( expression );
			map.CreateMap();
			map.ReIndex( GetMaxIndex() + 1 );
			var reference = new CsvPropertyReferenceMap( property, map );
			ReferenceMaps.Add( reference );
			return reference;
		}
	}
}
