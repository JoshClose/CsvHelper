using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CsvHelper
{
	/// <summary>
	/// Function that is called when a reading exception occurs.
	/// The default function will re-throw the given exception. If you want to ignore
	/// reading exceptions, you can supply your own function to do other things like
	/// logging the issue.
	/// </summary>
	public delegate bool ReadingExceptionOccurred(ReadingExceptionOccurredArgs args);

	/// <summary>
	/// ReadingExceptionOccurred args.
	/// </summary>
	public readonly struct ReadingExceptionOccurredArgs
	{
		/// <summary>
		/// The exception.
		/// </summary>
		public CsvHelperException Exception { get; init; }
	}
}
