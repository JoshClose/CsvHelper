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
	/// <see cref="BadDataFound"/> args.
	/// </summary>
	public readonly struct BadDataFoundArgs
	{
		/// <summary>
		/// The field.
		/// </summary>
		public readonly string Field { get; init; }

		/// <summary>
		/// The context.
		/// </summary>
		public readonly CsvContext Context { get; init; }

		/// <summary>
		/// Creates a new instance of BadDataFoundArgs.
		/// </summary>
		/// <param name="field">The field.</param>
		/// <param name="context">The context</param>
		public BadDataFoundArgs(string field, CsvContext context)
		{
			Field = field;
			Context = context;
		}
	}
}
