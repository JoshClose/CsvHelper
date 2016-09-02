using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using CsvHelper.Configuration;

namespace CsvHelper.TypeConversion
{
	/// <summary>
	/// Converts an <see cref="IDictionary"/> to and from a <see cref="string"/>.
	/// </summary>
    public class IDictionaryConverter : DefaultTypeConverter
    {
		/// <summary>
		/// Converts the object to a string.
		/// </summary>
		/// <param name="value">The object to convert to a string.</param>
		/// <param name="row">The <see cref="ICsvWriterRow"/> for the current record.</param>
		/// <param name="propertyMapData">The <see cref="CsvPropertyMapData"/> for the property being written.</param>
		/// <returns>The string representation of the object.</returns>
		public override string ConvertToString( object value, ICsvWriterRow row, CsvPropertyMapData propertyMapData )
		{
			var dictionary = value as IDictionary;
			if( dictionary == null )
			{
				return base.ConvertToString( value, row, propertyMapData );
			}

			foreach( DictionaryEntry entry in dictionary )
			{
				row.WriteField( entry.Value );
			}

			return null;
		}

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

			var dictionary = new Dictionary<string, string>();
			for( var i = propertyMapData.Index; i <= indexEnd; i++ )
			{
				dictionary.Add( row.FieldHeaders[i], row.GetField( i ) );
			}

			return dictionary;
		}

		/// <summary>
		/// Determines whether this instance [can convert from] the specified type.
		/// </summary>
		/// <param name="type">The type.</param>
		/// <returns>
		///   <c>true</c> if this instance [can convert from] the specified type; otherwise, <c>false</c>.
		/// </returns>
		public override bool CanConvertFrom( Type type )
		{
			// We only care about strings.
			return type == typeof( string );
		}
	}
}
