using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CsvHelper
{
	/// <summary>
	/// Function that is used to determine if a field should get quoted when writing.
	/// </summary>
	public delegate bool ShouldQuote(ShouldQuoteArgs args);

	/// <summary>
	/// ShouldQuote args.
	/// </summary>
	public readonly struct ShouldQuoteArgs
	{
		/// <summary>
		/// The field.
		/// </summary>
		public readonly string Field;

		/// <summary>
		/// The field type.
		/// </summary>
		public readonly Type FieldType;

		/// <summary>
		/// The row.
		/// </summary>
		public readonly IWriterRow Row;

		/// <summary>
		/// Creates a new instance of ShouldQuoteArgs.
		/// </summary>
		/// <param name="field">The field.</param>
		/// <param name="fieldType">The field type.</param>
		/// <param name="row">The row.</param>
		public ShouldQuoteArgs(string field, Type fieldType, IWriterRow row)
		{
			Field = field;
			FieldType = fieldType;
			Row = row;
		}
	}
}
