using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CsvHelper
{
	/// <summary>
	/// Function that determines whether to skip the given record or not.
	/// </summary>
	public delegate bool ShouldSkipRecord(ShouldSkipRecordArgs args);

	/// <summary>
	/// ShouldSkipRecord args.
	/// </summary>
	public readonly struct ShouldSkipRecordArgs
	{
		/// <summary>
		/// The record.
		/// </summary>
		public string[] Record { get; init; }
	}
}
