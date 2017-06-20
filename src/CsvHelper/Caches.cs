using System;

namespace CsvHelper
{
	/// <summary>
	/// Types of caches.
	/// </summary>
	[Flags]
	public enum Caches
    {
		/// <summary>
		/// None.
		/// </summary>
		None = 0,

		/// <summary>
		/// Named index.
		/// </summary>
        NamedIndex = 1,

		/// <summary>
		/// Delegate that creates objects when reading.
		/// </summary>
		ReadRecord = 2,

		/// <summary>
		/// Delegate that writes objects to strings when writing.
		/// </summary>
		WriteRecord = 4,

		/// <summary>
		/// Type converter options.
		/// </summary>
		TypeConverterOptions = 8,

		/// <summary>
		/// Raw record.
		/// </summary>
		RawRecord = 16
    }
}
