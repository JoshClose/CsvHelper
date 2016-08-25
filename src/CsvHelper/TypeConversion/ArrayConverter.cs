using System;
using System.Collections;
using CsvHelper.Configuration;

namespace CsvHelper.TypeConversion
{
	/// <summary>
	/// Converts an <see cref="Array"/> to and from a <see cref="string"/>.
	/// </summary>
    public class ArrayConverter : IEnumerableConverter
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

			var arraySize = indexEnd - propertyMapData.Index + 1;

			var array = (Array)ReflectionHelper.CreateInstance( propertyMapData.Property.PropertyType, arraySize );

			var type = propertyMapData.Property.PropertyType.GetElementType();
			var arrayIndex = 0;
			for( var i = propertyMapData.Index; i <= indexEnd; i++ )
			{
				array.SetValue( row.GetField( type, i ), arrayIndex );
				arrayIndex++;
			}

			return array;
		}
	}
}
