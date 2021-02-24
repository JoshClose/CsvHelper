using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CsvHelper
{
	/// <summary>
	/// Function that gets called when bad data is found.
	/// </summary>
	/// <param name="args">The args.</param>
	public delegate void BadDataFound(BadDataFoundArgs args);

	/// <summary>
	/// Information about the field that caused <see cref="BadDataFound"/> to be called.
	/// </summary>
	public readonly struct BadDataFoundArgs
	{
		/// <summary>
		/// The full field unedited.
		/// </summary>
		public readonly string Field { get; init; }

		/// <summary>
		/// The full row unedited.
		/// </summary>
		public readonly string RawRecord { get; init; }

		/// <summary>
		/// The context.
		/// </summary>
		public readonly CsvContext Context { get; init; }
	}
}
