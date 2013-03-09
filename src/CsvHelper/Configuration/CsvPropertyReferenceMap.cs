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
		private CsvPropertyMapCollection referenceProperties = new CsvPropertyMapCollection();

		/// <summary>
		/// Gets the property.
		/// </summary>
		public PropertyInfo Property
		{
			get { return property; }
		}

		/// <summary>
		/// Gets or sets the reference properties.
		/// </summary>
		/// <value>
		/// The reference properties.
		/// </value>
		public CsvPropertyMapCollection ReferenceProperties
		{
			get { return referenceProperties; }
			protected set { referenceProperties = value; }
		}

		/// <summary>
		/// Initializes a new instance of the <see cref="CsvPropertyReferenceMap"/> class.
		/// </summary>
		/// <param name="property">The property.</param>
		public CsvPropertyReferenceMap( PropertyInfo property )
		{
			this.property = property;
		}

		/// <summary>
		/// Initializes a new instance of the <see cref="CsvPropertyReferenceMap"/> class.
		/// </summary>
		/// <param name="type">The type.</param>
		/// <param name="property">The property.</param>
		public CsvPropertyReferenceMap( Type type, PropertyInfo property )
		{
			if( type != typeof( CsvClassMap ) )
			{
				throw new ArgumentException( "The type is not a CsvClassMap." );
			}

			this.property = property;
			var map = (CsvClassMap)Activator.CreateInstance(type);
			referenceProperties = map.PropertyMaps;
		}
	}
}
