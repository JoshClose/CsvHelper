// Copyright 2009-2015 Josh Close and Contributors
// This file is a part of CsvHelper and is dual licensed under MS-PL and Apache 2.0.
// See LICENSE.txt for details or visit http://www.opensource.org/licenses/ms-pl.html for MS-PL and http://opensource.org/licenses/Apache-2.0 for Apache 2.0.
// http://csvhelper.com

using System.IO;

namespace CsvHelper
{
	/// <summary>
	/// Defines methods used to serialize data into a CSV file.
	/// </summary>
	public interface ICsvSerializer : ISerializer
	{
		/// <summary>
		/// Gets the <see cref="TextWriter"/>.
		/// </summary>
		TextWriter TextWriter { get; }
	}
}
