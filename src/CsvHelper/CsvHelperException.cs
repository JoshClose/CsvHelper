// Copyright 2009-2017 Josh Close and Contributors
// This file is a part of CsvHelper and is dual licensed under MS-PL and Apache 2.0.
// See LICENSE.txt for details or visit http://www.opensource.org/licenses/ms-pl.html for MS-PL and http://opensource.org/licenses/Apache-2.0 for Apache 2.0.
// https://github.com/JoshClose/CsvHelper
using System;
using System.Collections.Generic;

namespace CsvHelper
{
	/// <summary>
	/// Represents errors that occur in CsvHelper.
	/// </summary>
	public class CsvHelperException : Exception
	{
		/// <summary>
		/// Gets the context used when reading.
		/// </summary>
		public ReadingContext ReadingContext { get; private set; }

		/// <summary>
		/// Gets the context used when writing.
		/// </summary>
		public WritingContext WritingContext { get; private set; }

		/// <summary>
		/// Initializes a new instance of the CsvHelperException class.
		/// </summary>
		internal protected CsvHelperException() : base() { }

		/// <summary>
		/// Initializes a new instance of the CsvHelperException class.
		/// </summary>
		/// <param name="message">The message that describes the error.</param>
		internal protected CsvHelperException( string message ) : base( message ) { }

		/// <summary>
		/// Initializes a new instance of the CsvHelperException class.
		/// </summary>
		/// <param name="message">The error message that explains the reason for the exception.</param>
		/// <param name="innerException">The exception that is the cause of the current exception, or a null reference (Nothing in Visual Basic) if no inner exception is specified.</param>
		internal protected CsvHelperException( string message, Exception innerException ) : base( message, innerException ) { }

		/// <summary>
		/// Initializes a new instance of the <see cref="CsvHelperException"/> class.
		/// </summary>
		public CsvHelperException( ReadingContext context )
		{
			ReadingContext = context;
		}

		/// <summary>
		/// Initializes a new instance of the <see cref="CsvHelperException"/> class.
		/// </summary>
		public CsvHelperException( WritingContext context )
		{
			WritingContext = context;
		}
		
		/// <summary>
		/// Initializes a new instance of the <see cref="CsvHelperException"/> class
		/// with a specified error message.
		/// </summary>
		/// <param name="context">The reading context.</param>
		/// <param name="message">The message that describes the error.</param>
		public CsvHelperException( ReadingContext context, string message ) : base( message )
		{
			ReadingContext = context;
		}

		/// <summary>
		/// Initializes a new instance of the <see cref="CsvHelperException"/> class
		/// with a specified error message and a reference to the inner exception that 
		/// is the cause of this exception.
		/// </summary>
		/// <param name="context">The reading context.</param>
		/// <param name="message">The error message that explains the reason for the exception.</param>
		/// <param name="innerException">The exception that is the cause of the current exception, or a null reference (Nothing in Visual Basic) if no inner exception is specified.</param>
		public CsvHelperException( ReadingContext context, string message, Exception innerException ) : base( message, innerException )
		{
			ReadingContext = context;
		}

		/// <summary>
		/// Initializes a new instance of the <see cref="CsvHelperException"/> class
		/// with a specified error message.
		/// </summary>
		/// <param name="context">The writing context.</param>
		/// <param name="message">The message that describes the error.</param>
		public CsvHelperException( WritingContext context, string message ) : base( message )
		{
			WritingContext = context;
		}

		/// <summary>
		/// Initializes a new instance of the <see cref="CsvHelperException"/> class
		/// with a specified error message and a reference to the inner exception that 
		/// is the cause of this exception.
		/// </summary>
		/// <param name="context">The writing context.</param>
		/// <param name="message">The error message that explains the reason for the exception.</param>
		/// <param name="innerException">The exception that is the cause of the current exception, or a null reference (Nothing in Visual Basic) if no inner exception is specified.</param>
		public CsvHelperException( WritingContext context, string message, Exception innerException ) : base( message, innerException )
		{
			WritingContext = context;
		}
	}
}
