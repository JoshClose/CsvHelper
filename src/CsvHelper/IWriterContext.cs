// Copyright 2009-2017 Josh Close and Contributors
// This file is a part of CsvHelper and is dual licensed under MS-PL and Apache 2.0.
// See LICENSE.txt for details or visit http://www.opensource.org/licenses/ms-pl.html for MS-PL and http://opensource.org/licenses/Apache-2.0 for Apache 2.0.
// https://github.com/JoshClose/CsvHelper
using System.Collections.Generic;
using System.IO;

namespace CsvHelper
{
	/// <summary>
	/// Defines context information used by the <see cref="ICsvWriter"/>.
	/// </summary>
	public interface IWriterContext
    {
		/// <summary>
		/// Gets a value indicating if the header has been written.
		/// </summary>
		bool HasHeaderBeenWritten { get; }

		/// <summary>
		/// Get the current record;
		/// </summary>
		List<string> Record { get; }

		/// <summary>
		/// Gets the current row.
		/// </summary>
		int Row { get; }

		/// <summary>
		/// Gets a value indicating if the <see cref="TextReader"/>
		/// should be left open when disposing.
		/// </summary>
		bool LeaveOpen { get; }
	}
}
