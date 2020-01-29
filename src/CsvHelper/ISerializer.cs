// Copyright 2009-2020 Josh Close and Contributors
// This file is a part of CsvHelper and is dual licensed under MS-PL and Apache 2.0.
// See LICENSE.txt for details or visit http://www.opensource.org/licenses/ms-pl.html for MS-PL and http://opensource.org/licenses/Apache-2.0 for Apache 2.0.
// https://github.com/JoshClose/CsvHelper
using System;
using CsvHelper.Configuration;
using System.Threading.Tasks;

namespace CsvHelper
{
	/// <summary>
	/// Defines methods used to serialize data into a CSV file.
	/// </summary>
	public interface ISerializer : IDisposable
#if NET47 || NETSTANDARD
		, IAsyncDisposable
#endif
	{
		/// <summary>
		/// Gets the writing context.
		/// </summary>
		WritingContext Context { get; }

		/// <summary>
		/// Gets the configuration.
		/// </summary>
		ISerializerConfiguration Configuration { get; }

		/// <summary>
		/// Writes a record to the CSV file.
		/// </summary>
		/// <param name="record">The record to write.</param>
		void Write( string[] record );

		/// <summary>
		/// Writes a record to the CSV file.
		/// </summary>
		/// <param name="record">The record to write.</param>
		Task WriteAsync( string[] record );

		/// <summary>
		/// Writes a new line to the CSV file.
		/// </summary>
		void WriteLine();

		/// <summary>
		/// Writes a new line to the CSV file.
		/// </summary>
		Task WriteLineAsync();
	}
}
