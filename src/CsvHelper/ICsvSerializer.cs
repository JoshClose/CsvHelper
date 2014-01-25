// Copyright 2009-2014 Josh Close and Contributors
// This file is a part of CsvHelper and is licensed under the MS-PL
// See LICENSE.txt for details or visit http://www.opensource.org/licenses/ms-pl.html
// http://csvhelper.com
using System;
using CsvHelper.Configuration;

namespace CsvHelper
{
	/// <summary>
	/// Defines methods used to serialize data into a CSV file.
	/// </summary>
	public interface ICsvSerializer : IDisposable
	{
		/// <summary>
		/// Gets the configuration.
		/// </summary>
		CsvConfiguration Configuration { get; }

		/// <summary>
		/// Writes a record to the CSV file.
		/// </summary>
		/// <param name="record">The record to write.</param>
		void Write( string[] record );
	}
}
