#region License
// Copyright 2009-2010 Josh Close
// This file is a part of CsvHelper and is licensed under the MS-PL
// See LICENSE.txt for details or visit http://www.opensource.org/licenses/ms-pl.html
#endregion
using System.IO;

namespace CsvHelper
{
	public static class CsvReaderFactory
	{
		/// <summary>
		/// Creates an ICsvReader using the given <see cref="StreamReader" />.
		/// </summary>
		/// <param name="reader"><see cref="StreamReader" /> containing the CSV file.</param>
		/// <returns>A new <see cref="ICsvReader" />.</returns>
		public static ICsvReader Create( StreamReader reader )
		{
			var parser = new CsvParser( reader );
			return new CsvReader( parser );
		}

		/// <summary>
		/// Creates an ICsvReader using the given file path.
		/// </summary>
		/// <param name="filePath">The path to the CSV file.</param>
		/// <returns>A new <see cref="ICsvReader" />.</returns>
		public static ICsvReader Create( string filePath )
		{
			var parser = new CsvParser( filePath );
			return new CsvReader( parser );
		}
	}
}
