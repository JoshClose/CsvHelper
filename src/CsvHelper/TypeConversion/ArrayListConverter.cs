#if !PCL && !NETSTANDARD
using System;
using System.Collections;
using System.Linq;
using CsvHelper.Configuration;

namespace CsvHelper.TypeConversion
{
	/// <summary>
	/// Converts an <see cref="ArrayList"/> to and from a <see cref="string"/>.
	/// </summary>
    public class ArrayListConverter : IEnumerableConverter
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
			var list = new ArrayList();

			if( propertyMapData.IsNameSet || row.Configuration.HasHeaderRecord && !propertyMapData.IsIndexSet )
			{
				// Use the name.
				var nameIndex = 0;
				while( true )
				{
					string field;
					if( !row.TryGetField( propertyMapData.Names.FirstOrDefault(), nameIndex, out field ) )
					{
						break;
					}

					list.Add( field );
					nameIndex++;
				}
			}
			else
			{
				// Use the index.
				var indexEnd = propertyMapData.IndexEnd < propertyMapData.Index
					? row.CurrentRecord.Length - 1
					: propertyMapData.IndexEnd;

				for( var i = propertyMapData.Index; i <= indexEnd; i++ )
				{
					string field;
					if( row.TryGetField( i, out field ) )
					{
						list.Add( field );
					}
				}
			}

			return list;
		}
	}
}
#endif
