// Copyright 2009-2024 Josh Close
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
		public readonly string Field;

		/// <summary>
		/// The full row unedited.
		/// </summary>
		public readonly string RawRecord;

		/// <summary>
		/// The context.
		/// </summary>
		public readonly CsvContext Context;

		/// <summary>
		/// Creates a new instance of BadDataFoundArgs.
		/// </summary>
		/// <param name="field">The full field unedited.</param>
		/// <param name="rawRecord">The full row unedited.</param>
		/// <param name="context">The context.</param>
		public BadDataFoundArgs(string field, string rawRecord, CsvContext context)
		{
			Field = field;
			RawRecord = rawRecord;
			Context = context;
		}
	}
}
