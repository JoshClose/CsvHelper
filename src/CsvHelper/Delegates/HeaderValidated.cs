using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CsvHelper
{
	/// <summary>
	/// Function that is called when a header validation check is ran. The default function
	/// will throw a <see cref="ValidationException"/> if there is no header for a given member mapping.
	/// You can supply your own function to do other things like logging the issue instead of throwing an exception.
	/// </summary>
	public delegate void HeaderValidated(HeaderValidatedArgs args);

	/// <summary>
	/// HeaderValidated args.
	/// </summary>
	public readonly struct HeaderValidatedArgs
	{
		/// <summary>
		/// The invalid headers.
		/// </summary>
		public InvalidHeader[] InvalidHeaders { get; init; }

		/// <summary>
		/// The context.
		/// </summary>
		public CsvContext Context { get; init; }

		/// <summary>
		/// Creates a new instance of HeaderValidatedArgs.
		/// </summary>
		/// <param name="invalidHeaders">The invalid headers.</param>
		/// <param name="context">The context.</param>
		public HeaderValidatedArgs(InvalidHeader[] invalidHeaders, CsvContext context)
		{
			InvalidHeaders = invalidHeaders;
			Context = context;
		}
	}
}
