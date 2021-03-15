using CsvHelper.TypeConversion;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CsvHelper.Pipeline
{
	/// <summary>
	/// Metadata for getting a field.
	/// </summary>
    public ref struct GetFieldMetadata
    {
		/// <summary>
		/// The field index.
		/// </summary>
		public int Index;

		/// <summary>
		/// The collection of possible field names.
		/// </summary>
		public string[] Names;

		/// <summary>
		/// The index of the name. This is used when multiple header names are the same.
		/// </summary>
		public int NameIndex;

		/// <summary>
		/// The type converter to turn the field from a string to it's type.
		/// </summary>
		public ITypeConverter TypeConverter;

		/// <summary>
		/// The field as a string.
		/// </summary>
		public string FieldString;

		/// <summary>
		/// The type of the field.
		/// </summary>
		public Type Type;

		/// <summary>
		/// The field converted to it's type.
		/// </summary>
		public object Field;

		/// <summary>
		/// A value indicating if the field is optional.
		/// <c>true</c> if the field is optional, otherwise <c>false</c>.
		/// </summary>
		public bool IsOptional;

		/// <summary>
		/// The header record.
		/// </summary>
		public string[] HeaderRecord;

		/// <summary>
		/// The system context.
		/// </summary>
		public CsvContext Context;
    }
}
