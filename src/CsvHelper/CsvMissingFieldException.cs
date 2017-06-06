// Copyright 2009-2017 Josh Close and Contributors
// This file is a part of CsvHelper and is dual licensed under MS-PL and Apache 2.0.
// See LICENSE.txt for details or visit http://www.opensource.org/licenses/ms-pl.html for MS-PL and http://opensource.org/licenses/Apache-2.0 for Apache 2.0.
// https://github.com/JoshClose/CsvHelper
using System;

namespace CsvHelper
{
    /// <summary>
    /// Represents an error caused because a field is missing
    /// in the header while reading a CSV file.
    /// </summary>
	public class CsvMissingFieldException : CsvReaderException
	{
		/// <summary>
		/// Initializes a new instance of the <see cref="CsvMissingFieldException"/> class.
		/// </summary>
		public CsvMissingFieldException() {}

		/// <summary>
		/// Initializes a new instance of the <see cref="CsvMissingFieldException"/> class
		/// with a specified error message.
		/// </summary>
		/// <param name="message">The message that describes the error.</param>
		public CsvMissingFieldException( string message ) : base( message ) {}

		/// <summary>
		/// Initializes a new instance of the <see cref="CsvMissingFieldException"/> class
		/// with a specified error message and a reference to the inner exception that 
		/// is the cause of this exception.
		/// </summary>
		/// <param name="message">The error message that explains the reason for the exception.</param>
		/// <param name="innerException">The exception that is the cause of the current exception, or a null reference (Nothing in Visual Basic) if no inner exception is specified.</param>
		public CsvMissingFieldException( string message, Exception innerException ) : base( message, innerException ) { }
	}
}
