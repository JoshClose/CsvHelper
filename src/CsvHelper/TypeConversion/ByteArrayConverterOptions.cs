using System;

namespace CsvHelper.TypeConversion
{
	/// <summary>
	/// Options for converting byte arrays.
	/// </summary>
	[Flags]
	public enum ByteArrayConverterOptions
	{
		/// <summary>
		/// No options.
		/// </summary>
		None = 0,

		// TypeOptions

		/// <summary>
		/// Hexadecimal encoding.
		/// </summary>
		Hexadecimal = 1,

		/// <summary>
		/// Base64 encoding.
		/// </summary>
		Base64 = 2,

		// HexFormattingOptions

		/// <summary>
		/// Use dashes in between hex values.
		/// </summary>
		HexDashes = 4,

		/// <summary>
		/// Prefix hex number with 0x.
		/// </summary>
		HexInclude0x = 8,
	}
}
