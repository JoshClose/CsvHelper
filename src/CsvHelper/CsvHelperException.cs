// Copyright 2009-2012 Josh Close
// This file is a part of CsvHelper and is licensed under the MS-PL
// See LICENSE.txt for details or visit http://www.opensource.org/licenses/ms-pl.html
// http://csvhelper.com
using System;
using System.Runtime.Serialization;

namespace CsvHelper
{
	/// <summary>
	/// Represents errors that occur in CsvHelper.
	/// </summary>
	public class CsvHelperException : Exception
	{
		/// <summary>
		/// Initializes a new instance of the <see cref="CsvHelperException"/> class.
		/// </summary>
		public CsvHelperException() {}

		/// <summary>
		/// Initializes a new instance of the <see cref="CsvHelperException"/> class
		/// with a specified error message.
		/// </summary>
		/// <param name="message">The message that describes the error.</param>
		public CsvHelperException( string message ) : base( message ) {}

		/// <summary>
		/// Initializes a new instance of the <see cref="CsvHelperException"/> class
		/// with a specified error message and a reference to the inner exception that 
		/// is the cause of this exception.
		/// </summary>
		/// <param name="message">The error message that explains the reason for the exception.</param>
		/// <param name="innerException">The exception that is the cause of the current exception, or a null reference (Nothing in Visual Basic) if no inner exception is specified.</param>
		public CsvHelperException( string message, Exception innerException ) : base( message, innerException ) {}

#if !WINRT_4_5 && !SILVERLIGHT
		/// <summary>
		/// Initializes a new instance of the <see cref="CsvHelperException"/> class
		/// with serialized data.
		/// </summary>
		/// <param name="info">The <see cref="SerializationInfo"/> that holds the serialized object data about the exception being thrown.</param>
		/// <param name="context">The <see cref="StreamingContext"/> that contains contextual information about the source or destination.</param>
		public CsvHelperException( SerializationInfo info, StreamingContext context ) : base( info, context ) {}
#endif
	}
}
