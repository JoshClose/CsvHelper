#region License
// Copyright 2009-2010 Josh Close
// This file is a part of CsvHelper and is licensed under the MS-PL
// See LICENSE.txt for details or visit http://www.opensource.org/licenses/ms-pl.html
// http://csvhelper.com
#endregion
namespace CsvHelper
{
	/// <summary>
	/// Options for the <see cref="CsvWriter"/>.
	/// </summary>
	public class CsvWriterOptions
	{
		private char delimiter = ',';
		private bool hasHeaderRecord = true;

		/// <summary>
		/// Gets or sets the delimiter used when
		/// writing the CSV file.
		/// </summary>
		public char Delimiter
		{
			get { return delimiter; }
			set { delimiter = value; }
		}

		/// <summary>
		/// Gets or sets a value indicating a header
		/// record should be written to the CSV file.
		/// </summary>
		public bool HasHeaderRecord
		{
			get { return hasHeaderRecord; }
			set { hasHeaderRecord = value; }
		}
	}
}
