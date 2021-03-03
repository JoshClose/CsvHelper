using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CsvHelper
{
	/// <summary>
	/// Function that converts a string into an object.
	/// </summary>
	/// <typeparam name="TMember">The type of the member.</typeparam>
	/// <param name="args">The args.</param>
	/// <returns>The class object.</returns>
	public delegate TMember ConvertFromString<TMember>(ConvertFromStringArgs args);

	/// <summary>
	/// <see cref="ConvertFromString{TMember}"/> args.
	/// </summary>
	public readonly struct ConvertFromStringArgs
	{
		/// <summary>
		/// The row.
		/// </summary>
		public readonly IReaderRow Row;

		/// <summary>
		/// Creates a new instance of ConvertFromStringArgs.
		/// </summary>
		/// <param name="row">The row.</param>
		public ConvertFromStringArgs(IReaderRow row)
		{
			Row = row;
		}
	}
}
