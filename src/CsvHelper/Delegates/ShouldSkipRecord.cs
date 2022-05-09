// Copyright 2009-2022 Josh Close
// This file is a part of CsvHelper and is dual licensed under MS-PL and Apache 2.0.
// See LICENSE.txt for details or visit http://www.opensource.org/licenses/ms-pl.html for MS-PL and http://opensource.org/licenses/Apache-2.0 for Apache 2.0.
// https://github.com/JoshClose/CsvHelper
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
		public readonly IReaderRow Row;

		/// <summary>
		/// Creates a new instance of ShouldSkipRecordArgs.
		/// </summary>
		/// <param name="row">The row.</param>
		public ShouldSkipRecordArgs(IReaderRow row)
		{
			Row = row;
		}
	}
}
