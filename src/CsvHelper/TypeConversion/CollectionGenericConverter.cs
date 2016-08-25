using System;
using System.Collections;
using System.Collections.ObjectModel;
using System.Reflection;
using CsvHelper.Configuration;

namespace CsvHelper.TypeConversion
{
	/// <summary>
	/// Converts a <see cref="Collection{T}"/> to and from a <see cref="string"/>.
	/// </summary>
	public class CollectionGenericConverter : IEnumerableConverter
    {
		/// <summary>
		/// Converts the string to an object.
		/// </summary>
		/// <param name="text">The string to convert to an object.</param>
		/// <param name="row">The <see cref="ICsvReaderRow"/> for the current record.</param>
		/// <param name="propertyMapData">The <see cref="CsvPropertyMapData"/> for the property being created.</param>
		/// <returns>The object created from the string.</returns>
		public override object ConvertFromString( string text, ICsvReaderRow row, CsvPropertyMapData propertyMapData )
		{
			var indexEnd = propertyMapData.IndexEnd < propertyMapData.Index
				? row.CurrentRecord.Length - 1
				: propertyMapData.IndexEnd;

			// Since we're using the PropertyType here, this converter can be used for multiple types
			// as long as they implement IList.
			var list = (IList)ReflectionHelper.CreateInstance( propertyMapData.Property.PropertyType );

			var type = propertyMapData.Property.PropertyType.GetGenericArguments()[0];
			for( var i = propertyMapData.Index; i <= indexEnd; i++ )
			{
				list.Add( row.GetField( type, i ) );
			}

			return list;
		}
	}
}
