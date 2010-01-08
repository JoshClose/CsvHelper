#region License
// Copyright 2009-2010 Josh Close
// This file is a part of CsvHelper and is licensed under the MS-PL
// See LICENSE.txt for details or visit http://www.opensource.org/licenses/ms-pl.html
#endregion
using System;
using System.ComponentModel;

namespace CsvHelper
{
	/// <summary>
	/// Factory for creating a <see cref="TypeConverter" />
	/// using the <see cref="TypeConverterAttribute" />.
	/// </summary>
	public static class TypeConverterFactory
	{
		/// <summary>
		/// Creates a <see cref="TypeConverter" /> from the given <see cref="PropertyDescriptor" />.
		/// If a <see cref="TypeConverterAttribute" /> exists, the specified <see cref="TypeConverter" />
		/// will be used. Otherwise the default converter is used.
		/// </summary>
		/// <param name="property">The <see cref="PropertyDescriptor" /> used to create the <see cref="TypeConverter" />.</param>
		/// <returns></returns>
		public static TypeConverter CreateConverter( PropertyDescriptor property )
		{
			var typeConverterAttribute = property.Attributes[typeof( TypeConverterAttribute )] as TypeConverterAttribute;
			TypeConverter converter = null;
			if( typeConverterAttribute != null )
			{
				var type = Type.GetType( typeConverterAttribute.ConverterTypeName, false );
				if( type != null )
				{
					converter = TypeDescriptor.CreateInstance( null, type, null, null ) as TypeConverter;
				}
			}
			if( converter == null )
			{
				converter = TypeDescriptor.GetConverter( property.PropertyType );
			}
			return converter;
		}
	}
}
