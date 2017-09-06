using System;

namespace CsvHelper.Configuration
{
	/// <summary>
	/// Options for trimming of fields.
	/// </summary>
	[Flags]
    public enum TrimOptions
    {
		/// <summary>
		/// No trimming.
		/// </summary>
        None = 0,

		/// <summary>
		/// Trims the whitespace around a field.
		/// </summary>
		Trim = 1,

		/// <summary>
		/// Trims the whitespace inside of quotes around a field.
		/// </summary>
		InsideQuotes = 2
    }
}
