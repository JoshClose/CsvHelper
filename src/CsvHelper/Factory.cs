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
	/// Creates CsvHelper classes.
	/// </summary>
	public class Factory : IFactory
	{
		/// <summary>
		/// Creates an <see cref="IParser"/>.
		/// </summary>
		/// <param name="reader">The text reader to use for the csv parser.</param>
		/// <param name="configuration">The configuration to use for the csv parser.</param>
		/// <returns>The created parser.</returns>
		public virtual IParser CreateParser(TextReader reader, Configuration.CsvConfiguration configuration)
		{
			return new CsvParser(reader, configuration);
		}

		/// <summary>
		/// Creates an <see cref="IParser" />.
		/// </summary>
		/// <param name="reader">The text reader to use for the csv parser.</param>
		/// <param name="cultureInfo">The culture information.</param>
		/// <returns>
		/// The created parser.
		/// </returns>
		public virtual IParser CreateParser(TextReader reader, CultureInfo cultureInfo)
		{
			return new CsvParser(reader, cultureInfo);
		}

		/// <summary>
		/// Creates an <see cref="IReader"/>.
		/// </summary>
		/// <param name="reader">The text reader to use for the csv reader.</param>
		/// <param name="configuration">The configuration to use for the reader.</param>
		/// <returns>The created reader.</returns>
		public virtual IReader CreateReader(TextReader reader, Configuration.CsvConfiguration configuration)
		{
			return new CsvReader(reader, configuration);
		}

		/// <summary>
		/// Creates an <see cref="IReader" />.
		/// </summary>
		/// <param name="reader">The text reader to use for the csv reader.</param>
		/// <param name="cultureInfo">The culture information.</param>
		/// <returns>
		/// The created reader.
		/// </returns>
		public virtual IReader CreateReader(TextReader reader, CultureInfo cultureInfo)
		{
			return new CsvReader(reader, cultureInfo);
		}

		/// <summary>
		/// Creates an <see cref="IReader"/>.
		/// </summary>
		/// <param name="parser">The parser used to create the reader.</param>
		/// <returns>The created reader.</returns>
		public virtual IReader CreateReader(IParser parser)
		{
			return new CsvReader(parser);
		}

		/// <summary>
		/// Creates an <see cref="IWriter"/>.
		/// </summary>
		/// <param name="writer">The text writer to use for the csv writer.</param>
		/// <param name="configuration">The configuration to use for the writer.</param>
		/// <returns>The created writer.</returns>
		public virtual IWriter CreateWriter(TextWriter writer, Configuration.CsvConfiguration configuration)
		{
			return new CsvWriter(writer, configuration);
		}

		/// <summary>
		/// Creates an <see cref="IWriter" />.
		/// </summary>
		/// <param name="writer">The text writer to use for the csv writer.</param>
		/// <param name="cultureInfo">The culture information.</param>
		/// <returns>
		/// The created writer.
		/// </returns>
		public virtual IWriter CreateWriter(TextWriter writer, CultureInfo cultureInfo)
		{
			return new CsvWriter(writer, cultureInfo);
		}

		/// <summary>
		/// Access point for fluent interface to dynamically build a <see cref="ClassMap{T}"/>
		/// </summary>
		/// <typeparam name="T">Type you will be making a class map for</typeparam>
		/// <returns>Options to further configure the <see cref="ClassMap{T}"/></returns>
		public IHasMap<T> CreateClassMapBuilder<T>()
		{
			return new ClassMapBuilder<T>();
		}
	}
}
