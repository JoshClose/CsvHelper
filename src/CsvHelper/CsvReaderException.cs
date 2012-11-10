// Copyright 2009-2012 Josh Close
// This file is a part of CsvHelper and is licensed under the MS-PL
// See LICENSE.txt for details or visit http://www.opensource.org/licenses/ms-pl.html
// http://csvhelper.com
using System;
using System.Runtime.Serialization;

namespace CsvHelper
{
	/// <summary>
	/// Represents errors that occur while reading a CSV file.
	/// </summary>
	public class CsvReaderException : CsvHelperException, ICsvReaderExceptionInfo
	{
		/// <summary>
		/// Gets the character position that the parser is currently on.
		/// </summary>
		public virtual long CharPosition { get; set; }

		/// <summary>
		/// Gets the byte position that the parser is currently on.
		/// </summary>
		public virtual long BytePosition { get; set; }

		/// <summary>
		/// Gets the row of the CSV file that the parser is currently on.
		/// </summary>
		public virtual int Row { get; set; }

		/// <summary>
		/// Gets the index of the field that the error occurred on. (0 based).
		/// </summary>
		/// <value>
		/// The index of the field.
		/// </value>
		public virtual int FieldIndex { get; set; }

		/// <summary>
		/// Gets the name of the field that the error occurred on.
		/// </summary>
		/// <value>
		/// The name of the field.
		/// </value>
		public virtual string FieldName { get; set; }

		/// <summary>
		/// Gets the value of the field that the error occurred on.
		/// </summary>
		/// <value>
		/// The field value.
		/// </value>
		public virtual string FieldValue { get; set; }

		/// <summary>
		/// Initializes a new instance of the <see cref="CsvReaderException"/> class.
		/// </summary>
		public CsvReaderException() {}

		/// <summary>
		/// Initializes a new instance of the <see cref="CsvReaderException"/> class
		/// with a specified error message.
		/// </summary>
		/// <param name="message">The message that describes the error.</param>
		public CsvReaderException( string message ) : base( message ) {}

		/// <summary>
		/// Initializes a new instance of the <see cref="CsvReaderException"/> class
		/// with a specified error message and a reference to the inner exception that 
		/// is the cause of this exception.
		/// </summary>
		/// <param name="message">The error message that explains the reason for the exception.</param>
		/// <param name="innerException">The exception that is the cause of the current exception, or a null reference (Nothing in Visual Basic) if no inner exception is specified.</param>
		public CsvReaderException( string message, Exception innerException ) : base( message, innerException ) {}

		/// <summary>
		/// Initializes a new instance of the <see cref="CsvReaderException"/> class
		/// with serialized data.
		/// </summary>
		/// <param name="info">The <see cref="SerializationInfo"/> that holds the serialized object data about the exception being thrown.</param>
		/// <param name="context">The <see cref="StreamingContext"/> that contains contextual information about the source or destination.</param>
		public CsvReaderException( SerializationInfo info, StreamingContext context ) : base( info, context ) { }
	}
}
