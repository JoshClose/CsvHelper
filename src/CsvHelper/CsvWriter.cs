// Copyright 2009-2015 Josh Close and Contributors
// This file is a part of CsvHelper and is dual licensed under MS-PL and Apache 2.0.
// See LICENSE.txt for details or visit http://www.opensource.org/licenses/ms-pl.html for MS-PL and http://opensource.org/licenses/Apache-2.0 for Apache 2.0.
// http://csvhelper.com

using System.IO;
using CsvHelper.Configuration;

#pragma warning disable 649
#pragma warning disable 169

namespace CsvHelper
{
	/// <summary>
	/// Used to write CSV files.
	/// </summary>
	public class CsvWriter : CsvWriter<ICsvSerializer>
	{
		/// <summary>
		/// Creates a new CSV writer using the given <see cref="TextWriter" />.
		/// </summary>
		/// <param name="writer">The writer used to write the CSV file.</param>
		public CsvWriter( TextWriter writer ) : base( new CsvSerializer( writer, new CsvConfiguration() ), false ) { }

		/// <summary>
		/// Creates a new CSV writer using the given <see cref="TextWriter"/>.
		/// </summary>
		/// <param name="writer">The writer used to write the CSV file.</param>
		/// <param name="leaveOpen">true to leave the reader open after the CsvReader object is disposed, otherwise false.</param>
		public CsvWriter( TextWriter writer, bool leaveOpen ) : base( new CsvSerializer( writer, new CsvConfiguration() ), leaveOpen ) { }

		/// <summary>
		/// Creates a new CSV writer using the given <see cref="TextWriter"/>.
		/// </summary>
		/// <param name="writer">The <see cref="StreamWriter"/> use to write the CSV file.</param>
		/// <param name="configuration">The configuration.</param>
		public CsvWriter( TextWriter writer, ICsvWriterConfiguration configuration ) : base( new CsvSerializer( writer, configuration ), false ) { }

		/// <summary>
		/// Creates a new CSV writer using the given <see cref="ICsvSerializer"/>.
		/// </summary>
		/// <param name="serializer">The serializer.</param>
		public CsvWriter( ICsvSerializer serializer ) : base( serializer, false ) { }
	}
}
