// Copyright 2009-2015 Josh Close and Contributors
// This file is a part of CsvHelper and is dual licensed under MS-PL and Apache 2.0.
// See LICENSE.txt for details or visit http://www.opensource.org/licenses/ms-pl.html for MS-PL and http://opensource.org/licenses/Apache-2.0 for Apache 2.0.
// http://csvhelper.com
using System.IO;
using CsvHelper.Configuration;
#if !NET_2_0
using System.Linq.Expressions;
using System;
#endif

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

#if !NET_2_0
        ClassMapBuilder<T> Map<T>(Expression<Func<T, object>> map);
        #endif
    }
}
