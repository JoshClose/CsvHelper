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
		public string Field { get; init; }

		/// <summary>
		/// The field type.
		/// </summary>
		public Type FieldType { get; init; }

		/// <summary>
		/// The row.
		/// </summary>
		public IWriterRow Row { get; init; }
	}
}
