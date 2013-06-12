// Copyright 2009-2013 Josh Close
// This file is a part of CsvHelper and is licensed under the MS-PL
// See LICENSE.txt for details or visit http://www.opensource.org/licenses/ms-pl.html
// http://csvhelper.com
using System.IO;
using CsvHelper.Configuration;

namespace CsvHelper
{
	/// <summary>
	/// Defines methods used to create
	/// CsvHelper classes.
	/// </summary>
	public interface ICsvFactory
	{
		/// <summary>
		/// Creates an <see cref="ICsvParser"/>.
		/// </summary>
		/// <param name="reader">The text reader to use for the csv parser.</param>
		/// <param name="configuration">The configuration to use for the csv parser.</param>
		/// <returns>The created parser.</returns>
		ICsvParser CreateParser( TextReader reader, CsvConfiguration configuration );

		/// <summary>
		/// Creates an <see cref="ICsvParser"/>.
		/// </summary>
		/// <param name="reader">The text reader to use for the csv parser.</param>
		/// <returns>The created parser.</returns>
		ICsvParser CreateParser( TextReader reader );

		/// <summary>
		/// Creates an <see cref="ICsvReader"/>.
		/// </summary>
		/// <param name="reader">The text reader to use for the csv reader.</param>
		/// <param name="configuration">The configuration to use for the reader.</param>
		/// <returns>The created reader.</returns>
		ICsvReader CreateReader( TextReader reader, CsvConfiguration configuration );

		/// <summary>
		/// Creates an <see cref="ICsvReader"/>.
		/// </summary>
		/// <param name="reader">The text reader to use for the csv reader.</param>
		/// <returns>The created reader.</returns>
		ICsvReader CreateReader( TextReader reader );

		/// <summary>
		/// Creates an <see cref="ICsvReader"/>.
		/// </summary>
		/// <param name="parser">The parser used to create the reader.</param>
		/// <returns>The created reader.</returns>
		ICsvReader CreateReader( ICsvParser parser );

		/// <summary>
		/// Creates an <see cref="ICsvWriter"/>.
		/// </summary>
		/// <param name="writer">The text writer to use for the csv writer.</param>
		/// <param name="configuration">The configuration to use for the writer.</param>
		/// <returns>The created writer.</returns>
		ICsvWriter CreateWriter( TextWriter writer, CsvConfiguration configuration );

		/// <summary>
		/// Creates an <see cref="ICsvWriter"/>.
		/// </summary>
		/// <param name="writer">The text writer to use for the csv writer.</param>
		/// <returns>The created writer.</returns>
		ICsvWriter CreateWriter( TextWriter writer );
	}
}
