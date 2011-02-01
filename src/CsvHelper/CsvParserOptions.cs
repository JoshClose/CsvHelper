#region License
// Copyright 2009-2010 Josh Close
// This file is a part of CsvHelper and is licensed under the MS-PL
// See LICENSE.txt for details or visit http://www.opensource.org/licenses/ms-pl.html
// http://csvhelper.com
#endregion
using System;

namespace CsvHelper
{
	/// <summary>
	/// Options for the <see cref="CsvParser" />.
	/// </summary>
	public class CsvParserOptions
	{
		private int bufferSize = 2048;
		private char delimiter = ',';

		/// <summary>
		/// The buffer size to use when
		/// reading the stream.
		/// </summary>
		public int BufferSize
		{
			get { return bufferSize; } 
			set { bufferSize = value; }
		}

		/// <summary>
		/// The number of fields the CSV file has.
		/// If this is known ahead of time, set
		/// to make parsing more efficient.
		/// </summary>
		public int FieldCount { get; set; }

		/// <summary>
		/// The delimiter used to separate fields
		/// of the CSV records.
		/// </summary>
		public char Delimiter
		{
			get { return delimiter; }
			set
			{
				if( value == '\n' )
				{
					throw new Exception( "Newline is not a valid delimiter." );
				}
				delimiter = value;
			}
		}

		/// <summary>
		/// True to allow '#' at the beginning of
		/// a line to denote a line that is commented 
		/// out. Otherwise, false.
		/// </summary>
		public bool AllowComments { get; set; }
	}
}
