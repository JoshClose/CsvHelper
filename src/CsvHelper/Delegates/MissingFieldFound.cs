using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CsvHelper
{
	/// <summary>
	/// Function that is called when a missing field is found. The default function will
	/// throw a <see cref="MissingFieldException"/>. You can supply your own function to do other things
	/// like logging the issue instead of throwing an exception.
	/// </summary>
	public delegate void MissingFieldFound(MissingFieldFoundArgs args);

	/// <summary>
	/// MissingFieldFound args.
	/// </summary>
	public readonly struct MissingFieldFoundArgs
	{
		/// <summary>
		/// The header names.
		/// </summary>
		public string[] HeaderNames { get; init; }

		/// <summary>
		/// The index.
		/// </summary>
		public int Index { get; init; }

		/// <summary>
		/// The context.
		/// </summary>
		public CsvContext Context { get; init; }

		/// <summary>
		/// Creates a new instance of MissingFieldFoundArgs.
		/// </summary>
		/// <param name="headerNames">The header names.</param>
		/// <param name="index">The index.</param>
		/// <param name="context">The context.</param>
		public MissingFieldFoundArgs(string[] headerNames, int index, CsvContext context)
		{
			HeaderNames = headerNames;
			Index = index;
			Context = context;
		}
	}
}
