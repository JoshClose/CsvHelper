// Copyright 2009-2015 Josh Close and Contributors
// This file is a part of CsvHelper and is dual licensed under MS-PL and Apache 2.0.
// See LICENSE.txt for details or visit http://www.opensource.org/licenses/ms-pl.html for MS-PL and http://opensource.org/licenses/Apache-2.0 for Apache 2.0.
// http://csvhelper.com

using System.IO;
using CsvHelper.Configuration;

namespace CsvHelper
{
	/// <summary>
	/// Reads data that was parsed from <see cref="CsvParser" />.
	/// </summary>
	public class CsvReader : CsvReader<ICsvParser>
	{

		/// <summary>
		/// Creates a new CSV reader using the given <see cref="TextReader"/>.
		/// </summary>
		/// <param name="reader">The reader.</param>
		public CsvReader( TextReader reader ) : base( new CsvParser( reader, new CsvConfiguration() ), false ) { }

		/// <summary>
		/// Creates a new CSV reader using the given <see cref="TextReader"/>.
		/// </summary>
		/// <param name="reader">The reader.</param>
		/// <param name="leaveOpen">true to leave the reader open after the CsvReader object is disposed, otherwise false.</param>
		public CsvReader( TextReader reader, bool leaveOpen ) : base( new CsvParser( reader, new CsvConfiguration() ), leaveOpen ) { }

		/// <summary>
		/// Creates a new CSV reader using the given <see cref="TextReader"/> and
		/// <see cref="CsvConfiguration"/> and <see cref="CsvParser"/> as the default parser.
		/// </summary>
		/// <param name="reader">The reader.</param>
		/// <param name="configuration">The configuration.</param>
		public CsvReader( TextReader reader, CsvConfiguration configuration ) : base( new CsvParser( reader, configuration ), false ) { }

		/// <summary>
		/// Creates a new CSV reader using the given <see cref="TextReader"/>.
		/// </summary>
		/// <param name="reader">The reader.</param>
		/// <param name="configuration">The configuration.</param>
		/// <param name="leaveOpen">true to leave the reader open after the CsvReader object is disposed, otherwise false.</param>
		public CsvReader( TextReader reader, CsvConfiguration configuration, bool leaveOpen ) : base( new CsvParser( reader, configuration ), leaveOpen ) { }

		/// <summary>
		/// Creates a new CSV reader using the given <see cref="ICsvParser" />.
		/// </summary>
		/// <param name="parser">The <see cref="ICsvParser" /> used to parse the CSV file.</param>
		public CsvReader( ICsvParser parser ) : base( parser, false ) { }
	}
}
