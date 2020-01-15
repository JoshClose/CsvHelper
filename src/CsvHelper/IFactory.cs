// Copyright 2009-2020 Josh Close and Contributors
// This file is a part of CsvHelper and is dual licensed under MS-PL and Apache 2.0.
// See LICENSE.txt for details or visit http://www.opensource.org/licenses/ms-pl.html for MS-PL and http://opensource.org/licenses/Apache-2.0 for Apache 2.0.
// https://github.com/JoshClose/CsvHelper
using System.Globalization;
using System.IO;
using CsvHelper.Configuration;

namespace CsvHelper
{
	/// <summary>
	/// Defines methods used to create
	/// CsvHelper classes.
	/// </summary>
	public interface IFactory
	{
		/// <summary>
		/// Creates an <see cref="IParser"/>.
		/// </summary>
		/// <param name="reader">The text reader to use for the csv parser.</param>
		/// <param name="configuration">The configuration to use for the csv parser.</param>
		/// <returns>The created parser.</returns>
		IParser CreateParser(TextReader reader, Configuration.CsvConfiguration configuration);

		/// <summary>
		/// Creates an <see cref="IParser" />.
		/// </summary>
		/// <param name="reader">The text reader to use for the csv parser.</param>
		/// <param name="cultureInfo">The culture information.</param>
		/// <returns>
		/// The created parser.
		/// </returns>
		IParser CreateParser(TextReader reader, CultureInfo cultureInfo);

		/// <summary>
		/// Creates an <see cref="IReader"/>.
		/// </summary>
		/// <param name="reader">The text reader to use for the csv reader.</param>
		/// <param name="configuration">The configuration to use for the reader.</param>
		/// <returns>The created reader.</returns>
		IReader CreateReader(TextReader reader, Configuration.CsvConfiguration configuration);

		/// <summary>
		/// Creates an <see cref="IReader" />.
		/// </summary>
		/// <param name="reader">The text reader to use for the csv reader.</param>
		/// <param name="cultureInfo">The culture information.</param>
		/// <returns>
		/// The created reader.
		/// </returns>
		IReader CreateReader(TextReader reader, CultureInfo cultureInfo);

		/// <summary>
		/// Creates an <see cref="IReader"/>.
		/// </summary>
		/// <param name="parser">The parser used to create the reader.</param>
		/// <returns>The created reader.</returns>
		IReader CreateReader(IParser parser);

		/// <summary>
		/// Creates an <see cref="IWriter"/>.
		/// </summary>
		/// <param name="writer">The text writer to use for the csv writer.</param>
		/// <param name="configuration">The configuration to use for the writer.</param>
		/// <returns>The created writer.</returns>
		IWriter CreateWriter(TextWriter writer, Configuration.CsvConfiguration configuration);

		/// <summary>
		/// Creates an <see cref="IWriter" />.
		/// </summary>
		/// <param name="writer">The text writer to use for the csv writer.</param>
		/// <param name="cultureInfo">The culture information.</param>
		/// <returns>
		/// The created writer.
		/// </returns>
		IWriter CreateWriter(TextWriter writer, CultureInfo cultureInfo);

		/// <summary>
		/// Provides a fluent interface for dynamically creating <see cref="ClassMap{T}"/>s 
		/// </summary>
		/// <typeparam name="T">Type of class to map</typeparam>
		/// <returns>Next available options</returns>
		IHasMap<T> CreateClassMapBuilder<T>();
	}
}
