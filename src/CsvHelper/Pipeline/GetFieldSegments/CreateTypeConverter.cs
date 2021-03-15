using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CsvHelper.Pipeline.GetFieldSegments
{
	/// <summary>
	/// Segement to create the type converter to be used to convert the string field to it's object type.
	/// </summary>
	public static class CreateTypeConverter
	{
		/// <inheritdoc/>
		public static void Execute(GetFieldMetadata metadata)
		{
			if (metadata.TypeConverter != null)
			{
				// A type converter was already specified.
				return;
			}

			metadata.TypeConverter = metadata.Context.TypeConverterCache.GetConverter(metadata.Type);
		}
	}
}
