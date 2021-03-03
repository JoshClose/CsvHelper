using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CsvHelper
{
	/// <summary>
	/// Function that converts an object into a string.
	/// </summary>
	/// <typeparam name="TClass">The type of the class.</typeparam>
	/// <param name="args">The args.</param>
	/// <returns>The string.</returns>
	public delegate string ConvertToString<TClass>(ConvertToStringArgs<TClass> args);

	/// <summary>
	/// <see cref="ConvertToString{TClass}"/> args.
	/// </summary>
	/// <typeparam name="TClass">The value to convert.</typeparam>
	public readonly struct ConvertToStringArgs<TClass>
	{
		/// <summary>
		/// The value to convert.
		/// </summary>
		public readonly TClass Value;

		/// <summary>
		/// Creates a new instance of ConvertToStringArgs{TClass}.
		/// </summary>
		/// <param name="value">The value to convert.</param>
		public ConvertToStringArgs(TClass value)
		{
			Value = value;
		}
	}
}
