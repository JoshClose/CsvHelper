// Copyright 2009-2013 Josh Close
// This file is a part of CsvHelper and is licensed under the MS-PL
// See LICENSE.txt for details or visit http://www.opensource.org/licenses/ms-pl.html
// http://csvhelper.com
using System;
using System.Reflection;

namespace CsvHelper.Configuration
{
	/// <summary>
	/// Mappinging info for a reference property mapping to a class.
	/// </summary>
	public class CsvPropertyReferenceMap
	{
		private readonly PropertyInfo property;

		/// <summary>
		/// Gets the property.
		/// </summary>
		public PropertyInfo Property
		{
			get { return property; }
		}

		/// <summary>
		/// Gets the mapping.
		/// </summary>
		public CsvClassMap Mapping { get; protected set; }

		/// <summary>
		/// Initializes a new instance of the <see cref="CsvPropertyReferenceMap"/> class.
		/// </summary>
		/// <param name="mapType">The <see cref="CsvClassMap"/> type to create to use for the reference map.</param>
		/// <param name="property">The property.</param>
		public CsvPropertyReferenceMap( Type mapType, PropertyInfo property )
			: this( property, ReflectionHelper.CreateInstance( mapType ) as CsvClassMap ) {}

		/// <summary>
		/// Initializes a new instance of the <see cref="CsvPropertyReferenceMap"/> class.
		/// </summary>
		/// <param name="property">The property.</param>
		/// <param name="mapping">The <see cref="CsvClassMap"/> to use for the reference map.</param>
		public CsvPropertyReferenceMap( PropertyInfo property, CsvClassMap mapping )
		{
			if( mapping == null )
			{
				throw new ArgumentNullException( "mapping" );
			}

			this.property = property;
			Mapping = mapping;
		}
	}
}
