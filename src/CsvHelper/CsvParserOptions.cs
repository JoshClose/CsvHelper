#region License
// Copyright 2009-2011 Josh Close
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
		private char quote = '"';

		/// <summary>
		/// Gets or sets the buffer size to use when
		/// reading the stream.
		/// </summary>
		public int BufferSize
		{
			get { return bufferSize; } 
			set { bufferSize = value; }
		}

		/// <summary>
		/// Gets or sets the number of fields the CSV file has.
		/// If this is known ahead of time, set
		/// to make parsing more efficient.
		/// </summary>
		public int FieldCount { get; set; }

		/// <summary>
		/// Gets or sets the delimiter used to separate fields
		/// of the CSV records.
		/// </summary>
		public char Delimiter
		{
			get { return delimiter; }
			set
			{
				if( value == '\n' )
				{
					throw new CsvHelperException( "Newline is not a valid delimiter." );
				}
				if ( value == '\r' )
				{
					throw new CsvHelperException( "Carriage return is not a valid delimiter." );
				}
				if( value == '\0' )
				{
					throw new CsvHelperException( "Null is not a valid delimiter." );
				}
				if( value == quote )
				{
					throw new CsvHelperException( "You can not use the quote as a delimiter." );
				}
				delimiter = value;
			}
		}

		/// <summary>
		/// Gets or sets the quote used to quote fields.
		/// </summary>
		public char Quote
		{
			get { return quote; }
			set
			{
				if( value == '\n' )
				{
					throw new CsvHelperException( "Newline is not a valid quote." );
				}
				if( value == '\r' )
				{
					throw new CsvHelperException( "Carriage return is not a valid quote." );
				}
				if( value == '\0' )
				{
					throw new CsvHelperException( "Null is not a valid quote." );
				}
				if( value == delimiter )
				{
					throw new CsvHelperException( "You can not use the delimiter as a quote." );
				}
				quote = value;
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
